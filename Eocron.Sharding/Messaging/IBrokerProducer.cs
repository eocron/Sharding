using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Messaging
{
    public interface IBrokerProducer<TMessage> : IDisposable
    {
        Task PublishAsync(IEnumerable<BrokerMessage<TMessage>> messages, CancellationToken ct);
    }
}