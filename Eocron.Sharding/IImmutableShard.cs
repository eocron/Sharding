using Eocron.Sharding.Jobs;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding
{
    public interface IImmutableShard :
        IShardStateProvider,
        ILifetimeProvider
    {
        string Id { get; }
    }
}