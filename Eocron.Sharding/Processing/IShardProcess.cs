using Eocron.Sharding.Jobs;

namespace Eocron.Sharding.Processing
{
    public interface IShardProcess<in TInput, TOutput, TError> :
        IShardOutputProvider<TOutput, TError>,
        IShardInputManager<TInput>,
        IImmutableShardProcess,
        IJob
    {

    }
}