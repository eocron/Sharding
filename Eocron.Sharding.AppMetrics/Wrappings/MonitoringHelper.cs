using System;
using System.Collections.Generic;
using System.Linq;
using App.Metrics;

namespace Eocron.Sharding.AppMetrics.Wrappings
{
    public static class MonitoringHelper
    {
        public static T CreateProcessOptions<T>(string name, Action<T> configure = null,
            IReadOnlyDictionary<string, string> tags = null) where T : MetricValueOptionsBase, new()
        {
            return CreateOptions<T>("shard_process", name, configure, tags);
        }
        public static T CreateShardOptions<T>(string name, Action<T> configure = null,
            IReadOnlyDictionary<string, string> tags = null) where T : MetricValueOptionsBase, new()
        {
            return CreateOptions<T>("shard", name, configure, tags);
        }

        public static T CreateOptions<T>(string context, string name, Action<T> configure = null,
            IReadOnlyDictionary<string, string> tags = null) where T : MetricValueOptionsBase, new()
        {
            var result = new T
            {
                Context = context,
                Name = name,
                MeasurementUnit = Unit.Events,
                Tags = tags.ToMetricTags()
            };
            configure?.Invoke(result);
            return result;
        }

        public static MetricTags ToMetricTags(this IReadOnlyDictionary<string, string> tags)
        {
            var result = new MetricTags();
            if (tags != null && tags.Any())
                result = MetricTags.Concat(result,
                    tags.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value?.ToLowerInvariant()));

            return result;
        }

        public static T CreateBrokerOptions<T>(string name, Action<T> configure = null,
            IReadOnlyDictionary<string, string> tags = null) where T : MetricValueOptionsBase, new()
        {
            return CreateOptions<T>("broker", name, configure, tags);
        }
    }
}