using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Pools;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Messaging
{
    public sealed class BrokerShardProcessorJob<TInput, TOutput, TError> : IJob
    {
        private readonly IBrokerConsumerFactory<string, TInput> _consumerProvider;
        private readonly IBrokerProducerFactory _outputProducerProvider;
        private readonly IBrokerProducerFactory _errorProducerProvider;
        private readonly IShardManager<TInput, TOutput, TError> _shardManager;
        private readonly ILogger _logger;
        private readonly TimeSpan _reserveWaitInterval;
        private readonly TimeSpan _reserveTimeout;

        public BrokerShardProcessorJob(
            IBrokerConsumerFactory<string, TInput> consumerProvider, 
            IBrokerProducerFactory outputProducerProvider,
            IBrokerProducerFactory errorProducerProvider,
            IShardManager<TInput, TOutput, TError> shardManager,
            ILogger logger)
        {
            _consumerProvider = consumerProvider;
            _outputProducerProvider = outputProducerProvider;
            _errorProducerProvider = errorProducerProvider;
            _shardManager = shardManager;
            _logger = logger;
            _reserveWaitInterval = TimeSpan.FromMilliseconds(1);
            _reserveTimeout = TimeSpan.FromSeconds(1);
        }

        public void Dispose()
        {
            
        }

        public async Task RunAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            using var consumer = _consumerProvider.CreateConsumer();
            using var outputProducer = _outputProducerProvider.CreateProducer<string, TOutput>();
            using var errorProducer = _errorProducerProvider.CreateProducer<string, TError>();
            await foreach (var batch in consumer.GetConsumerAsyncEnumerable(ct).ConfigureAwait(false))
            {
                int count = 0;
                await ProcessAsync(batch.Select(x =>
                {
                    Interlocked.Increment(ref count);
                    return x;
                }), outputProducer, errorProducer, ct).ConfigureAwait(false);
                _logger.LogInformation("Processed {count} messages.", count);
                await consumer.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task ProcessAsync(IEnumerable<BrokerMessage<string, TInput>> messages, IBrokerProducer<string, TOutput> outputProducer, IBrokerProducer<string, TError> errorProducer, CancellationToken ct)
        {
            using var reserveTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reserveTimeoutCts.CancelAfter(_reserveTimeout);
            var shard = await _shardManager.ReserveFreeAsync(ct, reserveWaitInterval: _reserveWaitInterval).ConfigureAwait(false);
            try
            {
                await shard.PublishAndHandleUntilReadyAsync(
                        messages.Select(x => x.Message),
                        async (batch, xct) =>
                        {
                            await outputProducer.PublishAsync(
                                    batch.Select(x => new BrokerMessage<string, TOutput>
                                    {
                                        Key = Guid.NewGuid().ToString(),
                                        Message = x.Value,
                                        Timestamp = DateTime.UtcNow
                                    }), xct)
                                .ConfigureAwait(false);
                        },
                        async (batch, xct) =>
                        {
                            await errorProducer.PublishAsync(
                                    batch.Select(x => new BrokerMessage<string, TError>
                                    {
                                        Key = Guid.NewGuid().ToString(),
                                        Message = x.Value,
                                        Timestamp = DateTime.UtcNow
                                    }), xct)
                                .ConfigureAwait(false);
                        },
                        ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                _shardManager.Return(shard);
            }
        }
    }
}
