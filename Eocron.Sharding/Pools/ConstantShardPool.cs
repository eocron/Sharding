using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.DataStructures;
using Eocron.Sharding.Jobs;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Pools
{
    public sealed class ConstantShardPool<TInput, TOutput, TError> : IShardPool<TInput, TOutput, TError>, IJob
    {
        public ConstantShardPool(
            ILogger logger, 
            IShardFactory<TInput, TOutput, TError> factory, 
            int size,
            TimeSpan priorityRecalculationInterval,
            TimeSpan checkTimeout)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _size = size;
            _unreserved =
                new PriorityDictionaryThreadSafetyGuard<string, long, IShard<TInput, TOutput, TError>>(
                    new PriorityDictionary<string, long, IShard<TInput, TOutput, TError>>(
                        StringComparer.InvariantCultureIgnoreCase));
            _immutable = new ConcurrentDictionary<string, IImmutableShard>(StringComparer.InvariantCultureIgnoreCase);
            _recalculateJob =
                new RestartUntilCancelledJob(
                    new RecalculatePrioritiesJob(
                        _unreserved,
                        _immutable,
                        logger,
                        checkTimeout),
                    logger,
                    priorityRecalculationInterval,
                    priorityRecalculationInterval);
        }

        public void Dispose()
        {
            _recalculateJob.Dispose();
        }

        public IEnumerable<IImmutableShard> GetAllShards()
        {
            return _immutable.Values;
        }

        public IImmutableShard GetShard(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            _immutable.TryGetValue(id, out var shard);
            return shard;
        }

        public bool IsExists(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            return _immutable.ContainsKey(id);
        }

        public void Return(IShard<TInput, TOutput, TError> shard)
        {
            _unreserved.Enqueue(shard.Id, long.MaxValue, shard);
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            var jobs = new Stack<IJob>();
            try
            {
                var tasks = Enumerable.Range(0, _size)
                    .Select(_ =>
                    {
                        var shard = _factory.CreateNewShard(Guid.NewGuid().ToString());
                        jobs.Push(shard);
                        _unreserved.Enqueue(shard.Id, long.MaxValue, shard);
                        _immutable.TryAdd(shard.Id, shard);
                        return (IJob)shard;
                    })
                    .Concat(new[] { _recalculateJob })
                    .Select(x => Task.Run(() => x.RunAsync(stoppingToken), stoppingToken))
                    .ToList();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                foreach (var shard in jobs) shard.Dispose();
                _unreserved.Clear();
                _immutable.Clear();
            }
        }

        public bool TryReserve(string id, out IShard<TInput, TOutput, TError> shard)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            return _unreserved.TryRemoveByKey(id, out shard);
        }

        public bool TryReserveFree(out string id, out IShard<TInput, TOutput, TError> shard)
        {
            return _unreserved.TryDequeue(out id, out shard);
        }

        private readonly ConcurrentDictionary<string, IImmutableShard> _immutable;
        private readonly IJob _recalculateJob;

        private readonly int _size;

        private readonly IPriorityDictionary<string, long, IShard<TInput, TOutput, TError>> _unreserved;
        private readonly IShardFactory<TInput, TOutput, TError> _factory;

        private class RecalculatePrioritiesJob : IJob
        {
            public RecalculatePrioritiesJob(
                IPriorityDictionary<string, long, IShard<TInput, TOutput, TError>> unreserved,
                IDictionary<string, IImmutableShard> immutable, ILogger logger,
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
}