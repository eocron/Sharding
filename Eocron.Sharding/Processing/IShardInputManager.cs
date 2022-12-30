using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Processing
{
    public interface IShardInputManager<in TInput>
    {
        /// <summary>
        ///     Publish messages to the shard
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task PublishAsync(IEnumerable<TInput> messages, CancellationToken ct);
    }
}