namespace Eocron.Sharding
{
    public interface IShardFactory<TInput, TOutput, TError>
    {
        IShard<TInput, TOutput, TError> CreateNewShard(string id);
    }
}