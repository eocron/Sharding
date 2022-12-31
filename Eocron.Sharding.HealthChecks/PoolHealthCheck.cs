using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Pools;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.HealthChecks
{
    public sealed class PoolHealthCheck : IHealthCheck
    {
        private readonly IImmutableShardProvider _provider;
        private readonly ILogger _logger;

        public PoolHealthCheck(IImmutableShardProvider provider, ILogger logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = new CancellationToken())
        {
            try
            {
                var results = await Task.WhenAll(
                        _provider
                            .GetAllShards()
                            .Select(async x =>
                                await x.IsReadyAsync(ct).ConfigureAwait(false)
                                && !await x.IsStoppedAsync(ct).ConfigureAwait(false)))
                    .ConfigureAwait(false);
                if (results.Any(x=> x))
                {
                    return HealthCheckResult.Healthy();
                }

                return HealthCheckResult.Degraded(description: "Pool is completely busy");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to check shard health");
                return new HealthCheckResult(context.Registration.FailureStatus, exception: e);
            }
        }
    }
}