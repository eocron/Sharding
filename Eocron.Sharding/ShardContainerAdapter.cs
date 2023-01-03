using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Messaging;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;

namespace Eocron.Sharding
{
    public sealed class ShardContainerAdapter<TInput, TOutput, TError> : IShard<TInput, TOutput, TError>
    {
        public ShardContainerAdapter(ServiceProvider container, string shardId)
        {
            _container = container;
            Id = shardId;
        }

        public void Dispose()
        {
            _container.Dispose();
        }

        public async Task<bool> IsReadyAsync(CancellationToken ct)
        {
            return await _container.GetRequiredService<IShardStateProvider>().IsReadyAsync(ct).ConfigureAwait(false);
        }

        public async Task PublishAsync(IEnumerable<BrokerMessage<TInput>> messages, CancellationToken ct)
        {
            await _container.GetRequiredService<IShardInputManager<TInput>>().PublishAsync(messages, ct).ConfigureAwait(false);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await new CompoundJob(_container.GetServices<IJob>().ToArray()).RunAsync(ct).ConfigureAwait(false);
        }

        public ChannelReader<BrokerMessage<TError>> Errors =>
            _container.GetRequiredService<IShardOutputProvider<TOutput, TError>>().Errors;

        public ChannelReader<BrokerMessage<TOutput>> Outputs =>
            _container.GetRequiredService<IShardOutputProvider<TOutput, TError>>().Outputs;

        public string Id { get; private set; }
        private readonly ServiceProvider _container;
        public async Task<bool> IsStoppedAsync(CancellationToken ct)
        {
            return await _container.GetRequiredService<ILifetimeProvider>().IsStoppedAsync(ct).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken ct)
        {
            await _container.GetRequiredService<ILifetimeManager>().StopAsync(ct).ConfigureAwait(false);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            await _container.GetRequiredService<ILifetimeManager>().StartAsync(ct).ConfigureAwait(false);
        }

        public async Task RestartAsync(CancellationToken ct)
        {
            await _container.GetRequiredService<ILifetimeManager>().RestartAsync(ct).ConfigureAwait(false);
        }
    }
}