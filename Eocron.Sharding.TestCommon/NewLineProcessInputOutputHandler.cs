using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Handlers;

namespace Eocron.Sharding.TestCommon
{
    public sealed class NewLineProcessInputOutputHandler : IProcessInputOutputHandler<string, string, string>
    {
        private readonly Process _process;
        private readonly SemaphoreSlim _semaphore;
        private readonly Channel<string> _outputs;
        private DateTime _deadline;
        private readonly Channel<string> _errors;
        private readonly TimeSpan _timeout;

        public NewLineProcessInputOutputHandler(Process process)
        {
            _process = process;
            _process.ErrorDataReceived += _process_ErrorDataReceived;
            _process.OutputDataReceived += _process_OutputDataReceived;
            _outputs = Channel.CreateUnbounded<string>();
            _errors = Channel.CreateUnbounded<string>();
            _semaphore = new SemaphoreSlim(1);
            _timeout = TimeSpan.FromMilliseconds(100);
        }

        private void _process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _outputs.Writer.TryWrite(e.Data);
                _deadline = DateTime.UtcNow + _timeout;
            }
        }

        private void _process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _errors.Writer.TryWrite(e.Data);
                _deadline = DateTime.UtcNow + _timeout;
            }
        }

        public bool IsReady()
        {
            return _semaphore.CurrentCount > 0;
        }

        public IAsyncEnumerable<string> ReadAllOutputsAsync(CancellationToken ct)
        {
            return _outputs.Reader.ReadAllAsync(ct);
        }

        public IAsyncEnumerable<string> ReadAllErrorsAsync(CancellationToken ct)
        {
            return _errors.Reader.ReadAllAsync(ct);
        }

        public async Task WriteInputsAsync(IEnumerable<string> items, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                foreach (var item in items)
                {
                    await _process.StandardInput.WriteLineAsync(item).ConfigureAwait(false);
                }

                await WhenFinished(ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task WhenFinished(CancellationToken ct)
        {
            _deadline = DateTime.UtcNow + _timeout;
            while (_deadline < DateTime.UtcNow)
            {
                await Task.Delay(_timeout, ct).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _process.ErrorDataReceived -= _process_ErrorDataReceived;
            _process.OutputDataReceived-= _process_OutputDataReceived;
            _semaphore.Dispose();
        }
    }
}