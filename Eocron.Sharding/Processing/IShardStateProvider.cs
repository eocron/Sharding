using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Processing
{
    public interface IShardStateProvider
    {
        /// <summary>
        ///     Checks if shard is ready to process data
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<bool> IsReadyAsync(CancellationToken ct);
    }
}