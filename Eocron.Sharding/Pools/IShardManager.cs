namespace Eocron.Sharding.Pools
{
    public interface IShardManager<TInput, TOutput, TError>
    {
        bool TryReserve(string id, out IShard<TInput, TOutput, TError> shard);

        bool TryReserveFree(out string id, out IShard<TInput, TOutput, TError> shard);

        bool HasFree();

        void Return(IShard<TInput, TOutput, TError> shard);
    }
}