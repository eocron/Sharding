using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.DataStructures;
using Eocron.Sharding.Jobs;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Pools
{
    internal sealed class RecalculatePrioritiesJob<TInput, TOutput, TError> : IJob
    {
        public RecalculatePrioritiesJob(
            IPriorityDictionary<string, long, IShard<TInput, TOutput, TError>> unreserved,
            IDictionary<string, IImmutableShard> immutable,
            ILogger logger,
            TimeSpan timeout)
        {
            _unreserved = unreserved;
            _immutable = immutable;
            _logger = logger;
            _timeout = timeout;
        }

        public void Dispose()
        {
        }

        public async Task RunAsync(CancellationToken stopToken)
        {
            foreach (var immutableShard in _immutable)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
                cts.CancelAfter(_timeout);
                var priority = long.MaxValue;
                try
                {
                    var isNotReady = !await immutableShard.Value.IsReadyAsync(cts.Token).ConfigureAwait(false);
                    var isStopped = await immutableShard.Value.IsStoppedAsync(cts.Token).ConfigureAwait(false);

                    priority = 0;
                    priority += isNotReady ? 1 : 0;
                    priority += isStopped ? 2 : 0;
                }
                catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update shard {shard_id} priority", immutableShard.Key);
                }

                if (_unreserved.TryUpdatePriority(immutableShard.Key, priority))
                {
                    _logger.LogInformation("Updated shard {shard_id} priority to {priority}",
                        immutableShard.Key, priority);
                }
            }
        }

        private readonly IDictionary<string, IImmutableShard> _immutable;
        private readonly ILogger _logger;
        private readonly IPriorityDictionary<string, long, IShard<TInput, TOutput, TError>> _unreserved;
        private readonly TimeSpan _timeout;
    }
}