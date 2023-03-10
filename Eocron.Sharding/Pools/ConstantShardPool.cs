using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.DataStructures;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Options;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Pools
{
    public sealed class ConstantShardPool<TInput, TOutput, TError> : IShardPool<TInput, TOutput, TError>, IJob
    {
        public ConstantShardPool(
            ILogger logger, 
            IShardFactory<TInput, TOutput, TError> factory, 
            ConstantShardPoolOptions options)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _unreserved =
                new PriorityDictionaryThreadSafetyGuard<string, long, IShard<TInput, TOutput, TError>>(
                    new PriorityDictionary<string, long, IShard<TInput, TOutput, TError>>(
                        StringComparer.InvariantCultureIgnoreCase));
            _immutable = new ConcurrentDictionary<string, IImmutableShard>(StringComparer.InvariantCultureIgnoreCase);
            _recalculateJob =
                new RestartUntilCancelledJob(
                    new RecalculatePrioritiesJob<TInput, TOutput, TError>(
                        _unreserved,
                        _immutable,
                        logger,
                        _options.PriorityCheckTimeout),
                    logger,
                    RestartPolicyOptions.Constant(_options.PriorityCheckInterval));
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
            await Task.Yield();
            var jobs = new Stack<IJob>();
            try
            {
                var tasks = Enumerable.Range(0, _options.PoolSize)
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

        private readonly IPriorityDictionary<string, long, IShard<TInput, TOutput, TError>> _unreserved;
        private readonly IShardFactory<TInput, TOutput, TError> _factory;
        private readonly ConstantShardPoolOptions _options;
    }
}