using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Processing
{
    public interface IShardInputManager<TInput>
    {
        /// <summary>
        ///     Publish messages to the shard
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task PublishAsync(IEnumerable<BrokerMessage<TInput>> messages, CancellationToken ct);
    }
}