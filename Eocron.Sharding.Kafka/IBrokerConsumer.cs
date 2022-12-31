using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Kafka
{
    public interface IBrokerConsumer<TKey, TMessage> : IDisposable
    {
        IAsyncEnumerable<IEnumerable<BrokerMessage<TKey, TMessage>>> GetConsumerAsyncEnumerable(CancellationToken ct);

        Task CommitAsync(CancellationToken ct);
    }
}