using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Options;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Jobs
{
    public sealed class RestartUntilCancelledJob : IJob
    {
        public RestartUntilCancelledJob(IJob inner, ILogger logger, RestartPolicyOptions options)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await Task.Yield();
            int successCount = 0;
            int errorCount = 0;
            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    _logger.LogDebug("Job running");
                    await _inner.RunAsync(ct).ConfigureAwait(false);
                    _logger.LogDebug("Job completed, running for {elapsed}, (s:{success_count},e:{error_count})", sw.Elapsed, successCount, errorCount);
                    errorCount = 0;
                    await Task.Delay(_options.OnSuccessDelay(successCount++), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogDebug("Job stopped");
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Job completed with error, running for {elapsed}, (s:{success_count},e:{error_count})", sw.Elapsed, successCount, errorCount);
                    try
                    {
                        successCount = 0;
                        await Task.Delay(_options.OnErrorDelay(errorCount++), ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }

        private readonly IJob _inner;
        private readonly ILogger _logger;
        private readonly RestartPolicyOptions _options;
    }
}