using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding.AppMetrics.Wrappings
{
    public class MonitoredShardStateProvider : IShardStateProvider
    {
        public MonitoredShardStateProvider(IShardStateProvider inner, IMetrics metrics,
            IReadOnlyDictionary<string, string> tags)
        {
            _inner = inner;
            _metrics = metrics;
            _errorCounterOptions = MonitoringHelper.CreateShardOptions<CounterOptions>("error_count", tags: tags);
            _readyForPublishOptions = MonitoringHelper.CreateShardOptions<GaugeOptions>("is_ready", tags: tags);
        }

        public async Task<bool> IsReadyAsync(CancellationToken ct)
        {
            try
            {
                var tmp = await _inner.IsReadyAsync(ct).ConfigureAwait(false);
                _metrics.Measure.Gauge.SetValue(_readyForPublishOptions, tmp ? 1 : 0);
                return tmp;
            }
            catch
            {
                _metrics.Measure.Counter.Increment(_errorCounterOptions);
                throw;
            }
        }

        private readonly CounterOptions _errorCounterOptions;
        private readonly GaugeOptions _readyForPublishOptions;
        private readonly IMetrics _metrics;
        private readonly IShardStateProvider _inner;
    }
}