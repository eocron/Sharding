using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Messaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Eocron.Sharding.RestApi.Pools
{
    public sealed class RemoteShard<TInput, TOutput, TError> : IShard<TInput, TOutput, TError>
    {
        public RemoteShard(
            ILogger logger,
            IHttpClientFactory httpClientFactory,
            string httpClientName,
            string shardId,
            JsonSerializerSettings settings,
            bool enableConsumer,
            TimeSpan consumerCheckInterval,
            TimeSpan onErrorInterval)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _httpClientName = httpClientName ?? throw new ArgumentNullException(nameof(httpClientName));
            Id = shardId ?? throw new ArgumentNullException(nameof(shardId));
            _settings = settings;
            _outputs = Channel.CreateUnbounded<BrokerMessage<TOutput>>();
            _errors = Channel.CreateUnbounded<BrokerMessage<TError>>();

            if (enableConsumer)
            {
                _consumer = new RestartUntilCancelledJob(
                    new ConsumerJob(
                        _httpClientFactory,
                        _httpClientName,
                        Id,
                        _settings,
                        _outputs,
                        _errors),
                    logger,
                    onErrorInterval,
                    consumerCheckInterval);
            }
        }

        public void Dispose()
        {
        }

        public async Task<bool> IsReadyAsync(CancellationToken ct)
        {
            return await GetAsync<bool>($"api/v1/shards/{Id}/is_ready", ct).ConfigureAwait(false);
        }

        public async Task<bool> IsStoppedAsync(CancellationToken ct)
        {
            return await GetAsync<bool>($"api/v1/shards/{Id}/is_stopped", ct).ConfigureAwait(false);
        }

        public async Task PublishAsync(IEnumerable<BrokerMessage<TInput>> messages, CancellationToken ct)
        {
            await PushAsync($"api/v1/shards/{Id}/publish", messages, ct).ConfigureAwait(false);
        }

        public async Task RestartAsync(CancellationToken ct)
        {
            await PushAsync($"api/v1/shards/{Id}/restart", ct).ConfigureAwait(false);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            if (_consumer != null)
            {
                await _consumer.RunAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task StartAsync(CancellationToken ct)
        {
            await PushAsync($"api/v1/shards/{Id}/start", ct).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken ct)
        {
            await PushAsync($"api/v1/shards/{Id}/stop", ct).ConfigureAwait(false);
        }

        private HttpClient CreateClient()
        {
            return _httpClientFactory.CreateClient(_httpClientName);
        }

        private async Task<T> GetAsync<T>(string path, CancellationToken ct)
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(path, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(content, _settings);
        }

        private async Task PushAsync<TRequest>(string path, TRequest obj, CancellationToken ct)
        {
            var json = JsonConvert.SerializeObject(obj, _settings);
            using var client = CreateClient();
            using var response = await client.PostAsync(path, new StringContent(json), ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private async Task PushAsync(string path, CancellationToken ct)
        {
            using var client = CreateClient();
            using var response = await client.PostAsync(path, new StringContent(""), ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        public ChannelReader<BrokerMessage<TError>> Errors => _errors.Reader;

        public ChannelReader<BrokerMessage<TOutput>> Outputs => _outputs.Reader;

        public string Id { get; }

        private readonly Channel<BrokerMessage<TError>> _errors;
        private readonly Channel<BrokerMessage<TOutput>> _outputs;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IJob _consumer;
        private readonly JsonSerializerSettings _settings;
        private readonly string _httpClientName;

        private class ConsumerJob : IJob
        {
            public ConsumerJob(
                IHttpClientFactory httpClientFactory,
                string httpClientName,
                string shardId,
                JsonSerializerSettings settings,
                Channel<BrokerMessage<TOutput>> outputChannel,
                Channel<BrokerMessage<TError>> errorChannel)
            {
                _httpClientFactory = httpClientFactory;
                _httpClientName = httpClientName;
                _shardId = shardId;
                _settings = settings;
                _outputChannel = outputChannel;
                _errorChannel = errorChannel;
            }

            public void Dispose()
            {
            }

            public async Task RunAsync(CancellationToken ct)
            {
                await Task.WhenAll(
                    Populate(_outputChannel, $"api/v1/{_shardId}/fetch_outputs", ct),
                    Populate(_errorChannel, $"api/v1/{_shardId}/fetch_errors", ct));
            }

            private async Task Populate<T>(Channel<T> channel, string path, CancellationToken ct)
            {
                var outputs = await PullAsync<List<T>>(path, ct).ConfigureAwait(false);
                foreach (var output in outputs) await channel.Writer.WriteAsync(output, ct).ConfigureAwait(false);
            }

            private async Task<TResponse> PullAsync<TResponse>(string path, CancellationToken ct)
            {
                using var client = _httpClientFactory.CreateClient(_httpClientName);
                using var response = await client.PostAsync(path, new StringContent(""), ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<TResponse>(content, _settings);
            }

            private readonly Channel<BrokerMessage<TError>> _errorChannel;
            private readonly Channel<BrokerMessage<TOutput>> _outputChannel;
            private readonly IHttpClientFactory _httpClientFactory;
            private readonly JsonSerializerSettings _settings;
            private readonly string _httpClientName;
            private readonly string _shardId;
        }
    }
}