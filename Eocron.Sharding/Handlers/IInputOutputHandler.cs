using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Handlers
{
    public interface IInputOutputHandler<TInput, TOutput, TError> : IHandlerStateProvider, IDisposable
    {
        IAsyncEnumerable<BrokerMessage<TOutput>> ReadAllOutputsAsync(CancellationToken ct);

        IAsyncEnumerable<BrokerMessage<TError>> ReadAllErrorsAsync(CancellationToken ct);

        Task WriteInputsAsync(IEnumerable<BrokerMessage<TInput>> items, CancellationToken ct);
    }
}