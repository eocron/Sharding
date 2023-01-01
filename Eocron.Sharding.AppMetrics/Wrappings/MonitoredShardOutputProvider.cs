using System.Collections.Generic;
using System.Threading.Channels;
using App.Metrics;
using App.Metrics.Histogram;
using Eocron.Sharding.Messaging;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding.AppMetrics.Wrappings
{
    public class MonitoredShardOutputProvider<TOutput, TError> : IShardOutputProvider<TOutput, TError>
    {
        public MonitoredShardOutputProvider(IShardOutputProvider<TOutput, TError> inner, IMetrics metrics,
            IReadOnlyDictionary<string, string> tags)
        {
            _inner = inner;
            _metrics = metrics;
            _outputDelayHistogramOptions =
                MonitoringHelper.CreateShardOptions<HistogramOptions>("output_read_delay_ms", tags: tags);
            _errorDelayHistogramOptions =
                MonitoringHelper.CreateShardOptions<HistogramOptions>("error_read_delay_ms", tags: tags);
        }

        public ChannelReader<BrokerMessage<TError>> Errors => new MonitoredChannelReader<BrokerMessage<TError>, TError>(
            _inner.Errors,
            _metrics,
            _errorDelayHistogramOptions);

        public ChannelReader<BrokerMessage<TOutput>> Outputs =>
            new MonitoredChannelReader<BrokerMessage<TOutput>, TOutput>(
                _inner.Outputs,
                _metrics,
                _outputDelayHistogramOptions);

        private readonly HistogramOptions _errorDelayHistogramOptions;
        private readonly HistogramOptions _outputDelayHistogramOptions;
        private readonly IMetrics _metrics;
        private readonly IShardOutputProvider<TOutput, TError> _inner;
    }
}