﻿using System;
using System.Collections.Generic;
using App.Metrics;
using Eocron.Sharding.AppMetrics.Jobs;
using Eocron.Sharding.AppMetrics.Wrappings;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Options;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.AppMetrics
{
    public static class ShardAppMetricsExtensions
    {
        public static ShardBuilder<TInput, TOutput, TError> WithAppMetrics<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder, 
            AppMetricsShardOptions options)
        {
            builder.Add((s, shardId)=> AddAppMetrics<TInput, TOutput, TError>(s, options));
            return builder;
        }
        private static IServiceCollection AddAppMetrics<TInput, TOutput, TError>(
            IServiceCollection container,
            AppMetricsShardOptions options)
        {
            return container
                .Replace<IShardInputManager<TInput>>((x, prev) =>
                    new MonitoredShardInputManager<TInput>(prev, x.GetRequiredService<IMetrics>(),
                        GetShardTags(x, options.Tags)))
                .Replace<IShardOutputProvider<TOutput, TError>>((x, prev) =>
                    new MonitoredShardOutputProvider<TOutput, TError>(prev, x.GetRequiredService<IMetrics>(),
                        GetShardTags(x, options.Tags)))
                .Replace<IJob>((x, prev) =>
                    new CompoundJob(
                        prev,
                        new RestartUntilCancelledJob(
                            new ShardMonitoringJob(
                                x.GetRequiredService<IShardStateProvider>(),
                                x.GetRequiredService<IProcessDiagnosticInfoProvider>(),
                                x.GetRequiredService<IMetrics>(),
                                options.CheckTimeout,
                                GetShardTags(x, options.Tags)),
                            x.GetRequiredService<ILogger>(),
                            RestartPolicyOptions.Constant(options.CheckInterval))));
        }

        private static IReadOnlyDictionary<string, string> GetShardTags(IServiceProvider provider,
            IEnumerable<KeyValuePair<string, string>> additionalTags)
        {
            return Merge(
                additionalTags,
                new[]
                {
                    new KeyValuePair<string, string>("shard_id", provider.GetRequiredService<IImmutableShardProcess>().Id)
                });
        }

        private static IReadOnlyDictionary<string, string> Merge(IEnumerable<KeyValuePair<string, string>> a,
            IEnumerable<KeyValuePair<string, string>> b)
        {
            var result = new Dictionary<string, string>();
            if (a != null)
            {
                foreach (var keyValuePair in a)
                {
                    result.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            if (b != null)
            {
                foreach (var keyValuePair in b)
                {
                    result.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            return result;
        }
    }
}