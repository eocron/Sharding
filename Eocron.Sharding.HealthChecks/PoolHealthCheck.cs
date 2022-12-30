using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.HealthChecks
{
    public sealed class PoolHealthCheck : IHealthCheck
    {
        private readonly Func<IEnumerable<IShardStateProvider>> _provider;
        private readonly ILogger _logger;

        public PoolHealthCheck(Func<IEnumerable<IShardStateProvider>> provider, ILogger logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                var results = await Task.WhenAll(_provider().Select(x=> x.IsReadyAsync(cancellationToken))).ConfigureAwait(false);
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