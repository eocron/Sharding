using Eocron.Sharding.Jobs;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding
{
    public interface IShard<in TInput, TOutput, TError> :
        IImmutableShard,
        IShardInputManager<TInput>,
        IShardOutputProvider<TOutput, TError>,
        ILifetimeManager,
        IJob
    {
    }
}