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
        private readonly IBrokerConsumerFactory<TInput> _consumerProvider;
        private readonly IBrokerProducerFactory _outputProducerProvider;
        private readonly IBrokerProducerFactory _errorProducerProvider;
        private readonly IShardManager<TInput, TOutput, TError> _shardManager;
        private readonly ILogger _logger;
        private readonly TimeSpan _reserveWaitInterval;
        private readonly TimeSpan _reserveTimeout;

        public BrokerShardProcessorJob(
            IBrokerConsumerFactory<TInput> consumerProvider, 
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
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            using var consumer = _consumerProvider.CreateConsumer();
            using var outputProducer = _outputProducerProvider.CreateProducer<TOutput>();
            using var errorProducer = _errorProducerProvider.CreateProducer<TError>();
            await foreach (var batch in consumer.GetConsumerAsyncEnumerable(ct).ConfigureAwait(false))
            {
                int count = 0;
                await ProcessAsync(batch.Select(x =>
                {
                    Interlocked.Increment(ref count);
                    return x;
                }), outputProducer, errorProducer, ct).ConfigureAwait(false);
                _logger.LogDebug("Processed {count} messages", count);
                await consumer.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task ProcessAsync(IEnumerable<BrokerMessage<TInput>> messages, IBrokerProducer<TOutput> outputProducer, IBrokerProducer<TError> errorProducer, CancellationToken ct)
        {
            using var reserveTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reserveTimeoutCts.CancelAfter(_reserveTimeout);
            var shard = await _shardManager.ReserveFreeAsync(ct, reserveWaitInterval: _reserveWaitInterval).ConfigureAwait(false);
            try
            {
                await shard.PublishAndHandleUntilReadyAsync(
                        messages,
                        outputProducer.PublishAsync,
                        errorProducer.PublishAsync,
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
