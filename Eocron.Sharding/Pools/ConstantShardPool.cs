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
            return _idToShardIndex.TryRemove(id, out shard);
        }

        public void Return(IShard<TInput, TOutput, TError> shard)
        {
            _idToShardIndex.TryAdd(shard.Id, shard);
        }

        public IEnumerable<IImmutableShard> GetAllShards()
        {
            return _idToShardIndex.Values;
        }

        public IImmutableShard GetShard(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            _idToShardIndex.TryGetValue(id, out var shard);
            return shard;
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
                        _idToShardIndex.TryAdd(shard.Id, shard);
                        return shard;
                    })
                    .Select(x => Task.Run(() => x.RunAsync(stoppingToken), stoppingToken))
                    .ToList();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                foreach (var shard in shards) shard.Dispose();
                _idToShardIndex.Clear();
            }
        }

        private readonly ConcurrentDictionary<string, IShard<TInput, TOutput, TError>> _idToShardIndex =
            new(StringComparer.InvariantCultureIgnoreCase);

        private readonly int _size;
        private readonly IShardFactory<TInput, TOutput, TError> _factory;
    }
}