using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Messaging
{
    public interface IBrokerConsumer<TMessage> : IDisposable
    {
        IAsyncEnumerable<IEnumerable<BrokerMessage<TMessage>>> GetConsumerAsyncEnumerable(CancellationToken ct);

        Task CommitAsync(CancellationToken ct);
    }
}