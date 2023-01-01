﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Gauge;
using Eocron.Sharding.AppMetrics.Wrappings;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.AppMetrics.Jobs
{
    public class ShardMonitoringJob : IJob
    {
        public ShardMonitoringJob(
            ILogger logger,
            IShardStateProvider stateProvider,
            IProcessDiagnosticInfoProvider infoProvider,
            IMetrics metrics,
            TimeSpan checkInterval,
            TimeSpan checkTimeout,
            IReadOnlyDictionary<string, string> tags)
        {
            _logger = logger;
            _stateProvider = stateProvider;
            _infoProvider = infoProvider;
            _metrics = metrics;
            _checkInterval = checkInterval;
            _workingSetGauge = MonitoringHelper.CreateProcessOptions<GaugeOptions>("working_set_bytes",
                x => { x.MeasurementUnit = Unit.Bytes; }, tags);
            _privateMemoryGauge = MonitoringHelper.CreateProcessOptions<GaugeOptions>("private_memory_bytes",
                x => { x.MeasurementUnit = Unit.Bytes; }, tags);
            _pagedMemoryGauge = MonitoringHelper.CreateProcessOptions<GaugeOptions>("paged_memory_bytes",
                x => { x.MeasurementUnit = Unit.Bytes; }, tags);
            _cpuPercentageGauge = MonitoringHelper.CreateProcessOptions<GaugeOptions>("cpu_load_percents",
                x => { x.MeasurementUnit = Unit.Percent; }, tags);
            _handleCountGauge = MonitoringHelper.CreateProcessOptions<GaugeOptions>("os_handle_count",
                x => { x.MeasurementUnit = Unit.Items; }, tags);
            _checkTimeout = checkTimeout;
        }

        public void Dispose()
        {
        }

        public async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using var cts = new CancellationTokenSource(_checkTimeout);
                try
                {
                    await OnCheck(cts.Token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to check shard status.");
                }

                try
                {
                    await Task.Delay(_checkInterval, ct).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }

        private static float GetCpuUsage(
            TimeSpan startTotalProcessorTime,
            TimeSpan endTotalProcessorTime,
            DateTime startCheckTime,
            DateTime endCheckTime)
        {
            var diffProcessorTime = endTotalProcessorTime.Ticks - startTotalProcessorTime.Ticks;
            var diffElapsedTime = (startCheckTime.Ticks - endCheckTime.Ticks) * Environment.ProcessorCount;

            var res = diffProcessorTime / (float)diffElapsedTime;
            if (float.IsInfinity(res) || float.IsNaN(res))
                return 0;
            if (res > 1)
                return 1;
            if (res < 0)
                return 0;
            return res;
        }

        private async Task OnCheck(CancellationToken ct)
        {
            await _stateProvider.IsReadyAsync(ct).ConfigureAwait(false);

            if (!_infoProvider.TryGetProcessDiagnosticInfo(out var info))
                info = new ProcessDiagnosticInfo();

            _metrics.Measure.Gauge.SetValue(_workingSetGauge, info.WorkingSet64);
            _metrics.Measure.Gauge.SetValue(_privateMemoryGauge, info.PrivateMemorySize64);
            _metrics.Measure.Gauge.SetValue(_pagedMemoryGauge, info.PagedMemorySize64);
            _metrics.Measure.Gauge.SetValue(_cpuPercentageGauge, SampleCpuUsage(info) * 100);
            _metrics.Measure.Gauge.SetValue(_handleCountGauge, info.HandleCount);
        }

        private float SampleCpuUsage(ProcessDiagnosticInfo info)
        {
            _lastCheckTime ??= DateTime.UtcNow;
            _lastTotalProcessorTime ??= TimeSpan.Zero;
            var currentTotalProcessorTime = info.TotalProcessorTime;
            var currentCheckTime = DateTime.UtcNow;
            var cpuPercents = GetCpuUsage(_lastTotalProcessorTime.Value, currentTotalProcessorTime,
                _lastCheckTime.Value, currentCheckTime);

            _lastCheckTime = currentCheckTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;

            return cpuPercents;
        }

        private readonly GaugeOptions _cpuPercentageGauge;
        private readonly GaugeOptions _privateMemoryGauge;
        private readonly GaugeOptions _pagedMemoryGauge;
        private readonly GaugeOptions _workingSetGauge;
        private readonly GaugeOptions _handleCountGauge;
        private readonly TimeSpan _checkTimeout;
        private readonly IMetrics _metrics;
        private readonly IProcessDiagnosticInfoProvider _infoProvider;
        private readonly ILogger _logger;
        private readonly IShardStateProvider _stateProvider;
        private readonly TimeSpan _checkInterval;
        private DateTime? _lastCheckTime;
        private TimeSpan? _lastTotalProcessorTime;
    }
}