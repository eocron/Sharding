namespace Eocron.Sharding.Pools
{
    public interface IShardPool<in TInput, TOutput, TError> : IShardProvider<TInput, TOutput, TError>
    {

    }
}