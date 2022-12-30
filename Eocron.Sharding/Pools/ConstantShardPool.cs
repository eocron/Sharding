using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Jobs;

namespace Eocron.Sharding.Pools
{
    public sealed class ConstantShardPool<TInput, TOutput, TError> : IShardPool<TInput, TOutput, TError>, IJob
    {
        public ConstantShardPool(IShardFactory<TInput, TOutput, TError> factory, int size)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _size = size;
        }

        public void Dispose()
        {
        }

        public bool TryReserve(string id, out IShard<TInput, TOutput, TError> shard)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            return _unreserved.TryRemove(id, out shard);
        }

        public void Return(IShard<TInput, TOutput, TError> shard)
        {
            _unreserved.TryAdd(shard.Id, shard);
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

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            var shards = new Stack<IShard<TInput, TOutput, TError>>();
            try
            {
                var tasks = Enumerable.Range(0, _size)
                    .Select(_ =>
                    {
                        var shard = _factory.CreateNewShard(Guid.NewGuid().ToString());
                        shards.Push(shard);
                        _unreserved.TryAdd(shard.Id, shard);
                        _immutable.TryAdd(shard.Id, shard);
                        return shard;
                    })
                    .Select(x => Task.Run(() => x.RunAsync(stoppingToken), stoppingToken))
                    .ToList();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                foreach (var shard in shards) shard.Dispose();
                _unreserved.Clear();
                _immutable.Clear();
            }
        }

        private readonly ConcurrentDictionary<string, IShard<TInput, TOutput, TError>> _unreserved =
            new(StringComparer.InvariantCultureIgnoreCase);

        private readonly ConcurrentDictionary<string, IImmutableShard> _immutable =
            new(StringComparer.InvariantCultureIgnoreCase);

        private readonly int _size;
        private readonly IShardFactory<TInput, TOutput, TError> _factory;
    }
}