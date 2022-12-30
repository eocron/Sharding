using Eocron.Sharding.Processing;

namespace Eocron.Sharding
{
    public interface IImmutableShard :
        IShardStateProvider,
        IShardLifetimeProvider
    {
        string Id { get; }
    }
}