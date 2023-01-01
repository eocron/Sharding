using Eocron.Sharding.Jobs;

namespace Eocron.Sharding.Processing
{
    public interface IShardProcessJob<TInput, TOutput, TError> :
        IShardOutputProvider<TOutput, TError>,
        IShardInputManager<TInput>,
        IImmutableShardProcess,
        IJob
    {

    }
}