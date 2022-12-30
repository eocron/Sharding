namespace Eocron.Sharding.Pools
{
    public interface IShardPool<TInput, TOutput, TError> : IShardProvider<TInput, TOutput, TError>, IShardManager<TInput, TOutput, TError>
    {

    }
}