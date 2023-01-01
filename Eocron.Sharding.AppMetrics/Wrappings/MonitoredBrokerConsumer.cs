using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.AppMetrics.Wrappings
{
    public sealed class MonitoredBrokerConsumer<TMessage> : IBrokerConsumer<TMessage>
    {
        public MonitoredBrokerConsumer(IBrokerConsumer<TMessage> inner, IMetrics metrics,
            IReadOnlyDictionary<string, string> tags = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _consumerDelayOptions =
                MonitoringHelper.CreateBrokerOptions<HistogramOptions>("consumed_timestamp_delay_ms", tags: tags);
            _consumerMsgCountOptions =
                MonitoringHelper.CreateBrokerOptions<CounterOptions>("consumed_message_count", tags: tags);
        }

        public async Task CommitAsync(CancellationToken ct)
        {
            await _inner.CommitAsync(ct);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async IAsyncEnumerable<IEnumerable<BrokerMessage<TMessage>>> GetConsumerAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var batch in _inner.GetConsumerAsyncEnumerable(ct).ConfigureAwait(false))
                yield return Wrap(batch);
        }

        private IEnumerable<BrokerMessage<TMessage>> Wrap(IEnumerable<BrokerMessage<TMessage>> batch)
        {
            DateTime? minDate = null;
            int count = 0;
            try
            {
                foreach (var brokerMessage in batch)
                {
                    if (minDate == null || brokerMessage.Timestamp < minDate.Value) minDate = brokerMessage.Timestamp;
                    Interlocked.Increment(ref count);
                    yield return brokerMessage;
                }
            }
            finally
            {
                if (minDate != null)
                {
                    var delay = DateTime.UtcNow - minDate.Value;
                    _metrics.Measure.Histogram.Update(_consumerDelayOptions,
                        delay.Ticks / TimeSpan.TicksPerMillisecond);
                }
                _metrics.Measure.Counter.Increment(_consumerMsgCountOptions, count);
            }
        }

        private readonly HistogramOptions _consumerDelayOptions;
        private readonly IBrokerConsumer<TMessage> _inner;
        private readonly IMetrics _metrics;
        private readonly CounterOptions _consumerMsgCountOptions;
    }
}