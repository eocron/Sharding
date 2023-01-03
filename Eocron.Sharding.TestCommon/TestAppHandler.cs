using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.TestCommon
{
    public sealed class TestAppHandler : IInputOutputHandler<string, string, string>
    {
        public TestAppHandler(Process process, Func<string> idGenerator)
        {
            _process = process;
            _idGenerator = idGenerator;
            _process.ErrorDataReceived += _process_ErrorDataReceived;
            _process.OutputDataReceived += _process_OutputDataReceived;
            _outputs = Channel.CreateUnbounded<BrokerMessage<string>>();
            _errors = Channel.CreateUnbounded<BrokerMessage<string>>();
            _semaphore = new SemaphoreSlim(1);
            _cooldown = new CooldownTask(TimeSpan.FromMilliseconds(100));
        }

        public void Dispose()
        {
            _process.ErrorDataReceived -= _process_ErrorDataReceived;
            _process.OutputDataReceived -= _process_OutputDataReceived;
            _semaphore.Dispose();
        }

        public bool IsReady()
        {
            return _semaphore.CurrentCount > 0;
        }

        public IAsyncEnumerable<BrokerMessage<string>> ReadAllErrorsAsync(CancellationToken ct)
        {
            return _errors.Reader.ReadAllAsync(ct);
        }

        public IAsyncEnumerable<BrokerMessage<string>> ReadAllOutputsAsync(CancellationToken ct)
        {
            return _outputs.Reader.ReadAllAsync(ct);
        }

        public async Task WriteInputsAsync(IEnumerable<BrokerMessage<string>> items, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                foreach (var item in items)
                    await _process.StandardInput.WriteLineAsync(item.Message).ConfigureAwait(false);

                await _cooldown.WaitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void _process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _errors.Writer.TryWrite(new BrokerMessage<string>
                {
                    Key = "err_" + _idGenerator(),
                    Message = e.Data,
                    Timestamp = DateTime.UtcNow
                });
                _cooldown.Refresh();
            }
        }

        private void _process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _outputs.Writer.TryWrite(new BrokerMessage<string>
                {
                    Key = "out_"+_idGenerator(),
                    Message = e.Data,
                    Timestamp = DateTime.UtcNow
                });
                _cooldown.Refresh();
            }
        }

        private readonly Channel<BrokerMessage<string>> _errors;
        private readonly Channel<BrokerMessage<string>> _outputs;
        private readonly Process _process;
        private readonly Func<string> _idGenerator;
        private readonly SemaphoreSlim _semaphore;
        private readonly CooldownTask _cooldown;
    }
}