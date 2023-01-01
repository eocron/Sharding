using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Histogram;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.AppMetrics.Wrappings
{
    public sealed class MonitoredBrokerConsumer<TKey, TMessage> : IBrokerConsumer<TKey, TMessage>
    {
        private readonly IBrokerConsumer<TKey, TMessage> _inner;
        private readonly IMetrics _metrics;
        private readonly HistogramOptions _consumerDelayOptions;

        public MonitoredBrokerConsumer(IBrokerConsumer<TKey, TMessage> inner, IMetrics metrics, IReadOnlyDictionary<string, string> tags = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _consumerDelayOptions = MonitoringHelper.CreateBrokerOptions<HistogramOptions>("consumer_delay_ms", tags: tags);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async IAsyncEnumerable<IEnumerable<BrokerMessage<TKey, TMessage>>> GetConsumerAsyncEnumerable([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var batch in _inner.GetConsumerAsyncEnumerable(ct).ConfigureAwait(false))
            {
                yield return Wrap(batch);
            }
        }

        private IEnumerable<BrokerMessage<TKey, TMessage>> Wrap(IEnumerable<BrokerMessage<TKey, TMessage>> batch)
        {
            DateTime? minDate = null;
            try
            {
                foreach (var brokerMessage in batch)
                {
                    if (minDate == null || brokerMessage.Timestamp < minDate.Value)
                    {
                        minDate = brokerMessage.Timestamp;
                    }

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
            }
        }

        public async Task CommitAsync(CancellationToken ct)
        {
            await _inner.CommitAsync(ct);
        }
    }
}
