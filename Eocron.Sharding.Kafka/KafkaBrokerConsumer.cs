using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Kafka
{
    public sealed class KafkaBrokerConsumer<TMessage> : IBrokerConsumer<TMessage>
    {
        public KafkaBrokerConsumer(ConsumerBuilder<string, TMessage> builder, int batchSize = 1,
            TimeSpan? batchTimeout = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize));

            _batchSize = batchSize;
            _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(1);
            _cts = new CancellationTokenSource();
            _consumer = new Lazy<IConsumer<string, TMessage>>(builder.Build,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task CommitAsync(CancellationToken ct)
        {
            _consumer.Value.Commit();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_consumer.IsValueCreated) _consumer.Value.Dispose();
            _cts.Dispose();
        }

        public async IAsyncEnumerable<IEnumerable<BrokerMessage<TMessage>>> GetConsumerAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
            var batch = new List<BrokerMessage<TMessage>>(_batchSize);
            var batchDeadline = DateTime.UtcNow + _batchTimeout;
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();
                var cr = _consumer.Value.Consume(_batchTimeout);

                if (cr == null)
                {
                    if (batch.Count > 0 && batchDeadline < DateTime.UtcNow)
                    {
                        yield return batch;
                        batchDeadline = DateTime.UtcNow + _batchTimeout;
                        batch = new List<BrokerMessage<TMessage>>(_batchSize);
                    }
                }
                else if (!cr.IsPartitionEOF)
                {
                    batch.Add(new BrokerMessage<TMessage>
                    {
                        Message = cr.Message.Value,
                        Key = cr.Message.Key,
                        Headers = cr.Message.Headers.ToDictionary(x => x.Key,
                            x => Encoding.UTF8.GetString(x.GetValueBytes()))
                    });
                    if (batch.Count == _batchSize || batchDeadline < DateTime.UtcNow)
                    {
                        yield return batch;
                        batchDeadline = DateTime.UtcNow + _batchTimeout;
                        batch = new List<BrokerMessage<TMessage>>(_batchSize);
                    }
                }
                else
                {
                    break;
                }
            }

            if (batch.Any())
                yield return batch;
        }

        private readonly CancellationTokenSource _cts;
        private readonly int _batchSize;
        private readonly Lazy<IConsumer<string, TMessage>> _consumer;
        private readonly TimeSpan _batchTimeout;
    }
}