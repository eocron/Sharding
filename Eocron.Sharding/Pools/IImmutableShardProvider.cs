using System.Collections.Generic;

namespace Eocron.Sharding.Pools
{
    public interface IImmutableShardProvider
    {
        IEnumerable<IImmutableShard> GetAllShards();

        IImmutableShard GetShard(string id);
    }
}