using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Kafka
{
    public interface IBrokerProducer<TKey, TMessage> : IDisposable
    {
        Task PublishAsync(IEnumerable<BrokerMessage<TKey, TMessage>> messages, CancellationToken ct);
    }
}