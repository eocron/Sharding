using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Handlers
{
    public interface IProcessInputOutputHandler<TInput, TOutput, TError> : IProcessStateProvider, IDisposable
    {
        IAsyncEnumerable<TOutput> ReadAllOutputsAsync(CancellationToken ct);

        IAsyncEnumerable<TError> ReadAllErrorsAsync(CancellationToken ct);

        Task WriteInputsAsync(IEnumerable<TInput> items, CancellationToken ct);
    }
}