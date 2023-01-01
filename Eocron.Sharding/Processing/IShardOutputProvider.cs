using System.Threading.Channels;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Processing
{
    public interface IShardOutputProvider<TOutput, TError>
    {
        /// <summary>
        ///     Errors coming from shard
        /// </summary>
        ChannelReader<BrokerMessage<TError>> Errors { get; }

        /// <summary>
        ///     Outputs coming from shard
        /// </summary>
        ChannelReader<BrokerMessage<TOutput>> Outputs { get; }
    }
}