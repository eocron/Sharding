using System;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.HealthChecks
{
    public sealed class ShardHealthCheck : IHealthCheck
    {
        private readonly IShardStateProvider _stateProvider;
        private readonly ILogger _logger;

        public ShardHealthCheck(IShardStateProvider stateProvider, ILogger logger)
        {
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                var result = await _stateProvider.IsReadyAsync(cancellationToken).ConfigureAwait(false);
                if (result)
                {
                    return HealthCheckResult.Healthy();
                }

                return HealthCheckResult.Degraded(description: "Shard is not ready");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to check shard health");
                return new HealthCheckResult(context.Registration.FailureStatus, exception: e);
            }
        }
    }
}
