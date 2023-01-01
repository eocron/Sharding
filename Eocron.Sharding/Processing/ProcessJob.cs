using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Configuration;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Processing
{
    public sealed class ProcessJob<TInput, TOutput, TError> : IShardProcess<TInput, TOutput, TError>
    {
        public ProcessJob(
            ProcessShardOptions options,
            IProcessInputOutputHandlerFactory<TInput, TOutput, TError> handlerFactory,
            ILogger logger,
            IChildProcessWatcher watcher = null,
            string id = null)
        {
            _options = options;
            _handlerFactory = handlerFactory;
            _logger = logger;
            _watcher = watcher;
            _outputs = Channel.CreateBounded<ShardMessage<TOutput>>(_options.OutputOptions);
            _errors = Channel.CreateBounded<ShardMessage<TError>>(_options.ErrorOptions);
            _publishSemaphore = new SemaphoreSlim(1);
            Id = id ?? $"process_shard_{Guid.NewGuid():N}";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _outputs.Writer.Complete(CreateShardDisposedException());
            _errors.Writer.Complete(CreateShardDisposedException());
            _publishSemaphore.Dispose();
            try
            {
                _current?.Dispose();
            }
            catch
            {

            }
            _disposed = true;
        }

        public Task<bool> IsReadyAsync(CancellationToken ct)
        {
            var process = _current;
            return Task.FromResult(process != null &&
                                   ProcessHelper.IsAlive(process.Process)
                                   && _publishSemaphore.CurrentCount > 0
                                   && process.Handler.IsReady());
        }

        public async Task PublishAsync(IEnumerable<TInput> messages, CancellationToken ct)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            await _publishSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var process = await GetRunningProcessAsync(ct).ConfigureAwait(false);
                using var logScope = BeginProcessLoggingScope(process.Process);
                await process.Handler.WriteInputsAsync(messages, ct).ConfigureAwait(false);
                if (ProcessHelper.IsDead(process.Process) && process.Process.ExitCode != 0) throw CreatePublishedWithErrorException(process.Process);
            }
            finally
            {
                _publishSemaphore.Release();
            }
        }

        public async Task RunAsync(CancellationToken stopToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            using var process = Process.Start(_options.StartInfo);
            var processId = ProcessHelper.GetId(process);
            process.EnableRaisingEvents = true;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            try
            {
                using var handler = _handlerFactory.CreateHandler(process);
                await using var register = cts.Token.Register(() => OnCancellation(process));
                using var logScope = BeginProcessLoggingScope(process);

                if (_watcher != null && processId != null)
                    await _watcher.ChildrenToWatch.Writer.WriteAsync(processId.Value, cts.Token).ConfigureAwait(false);

                _logger.LogInformation("Process {process_id} shard started", processId);
                await WaitUntilReady(handler, cts.Token).ConfigureAwait(false);
                _logger.LogInformation("Process {process_id} shard ready for publish", processId);
                var ioTasks = new[]
                {
                    ProcessOutputs(ct => handler.ReadAllOutputsAsync(ct), _outputs, processId, cts.Token),
                    ProcessOutputs(ct => handler.ReadAllErrorsAsync(ct), _errors, processId, cts.Token)
                };
                _current = new ProcessAndHandler(process, handler);
                await WaitUntilExit(process);

                cts.Cancel();
                await Task.WhenAll(ioTasks).ConfigureAwait(false);
            }
            finally
            {
                process.CancelErrorRead();
                process.CancelOutputRead();
            }
            var exitCode = ProcessHelper.GetExitCode(process) ?? -1;
            if (stopToken.IsCancellationRequested)
            {
                if (exitCode == 0)
                    _logger.LogInformation("Process {process_id} shard gracefully cancelled", processId);
                else
                    _logger.LogWarning("Process {process_id} shard cancelled with exit code {exit_code}",
                        processId, exitCode);
            }
            else
            {
                if (exitCode == 0)
                {
                    _logger.LogWarning("Process {process_id} shard suddenly stopped without error", processId);
                }
                else
                {
                    _logger.LogError("Process {process_id} shard suddenly stopped with exit code {exit_code}",
                        processId, exitCode);
                    throw CreateProcessExitCodeException(processId, exitCode);
                }
            }
        }

        public bool TryGetProcessDiagnosticInfo(out ProcessDiagnosticInfo info)
        {
            info = null;
            var current = _current;
            if (current == null)
                return false;
            info = ProcessHelper.DefaultIfNotFound(
                current.Process,
                x =>
                {
                    if (x.HasExited)
                        return null;

                    return new ProcessDiagnosticInfo
                    {
                        PagedMemorySize64 = x.PagedMemorySize64,
                        PrivateMemorySize64 = x.PrivateMemorySize64,
                        TotalProcessorTime = x.TotalProcessorTime,
                        WorkingSet64 = x.WorkingSet64,
                        ModuleName = x.MainModule?.ModuleName,
                        StartTime = x.StartTime,
                        HandleCount = x.HandleCount,
                    };
                },
                null);
            return info != null;
        }

        private IDisposable BeginProcessLoggingScope(Process process)
        {
            var processId = ProcessHelper.GetId(process);
            return _logger.BeginScope(new Dictionary<string, string>
            {
                { "process_id", processId?.ToString() },
                { "shard_id", Id }
            });
        }

        private Exception CreateProcessExitCodeException(int? processId, int? exitCode)
        {
            return new ProcessShardException(
                $"Process {processId} shard suddenly stopped with exit code {exitCode}.", Id, processId,
                exitCode);
        }

        private Exception CreatePublishedWithErrorException(Process process)
        {
            var processId = ProcessHelper.GetId(process);
            var exitCode = ProcessHelper.GetExitCode(process);
            return new ProcessShardException(
                $"Publish was successful but process crashed. Last time process {processId} stopped with exit code {exitCode}.",
                Id, processId, exitCode);
        }

        private Exception CreateShardDisposedException()
        {
            return new ObjectDisposedException(Id, "Shard is disposed.");
        }

        private Exception CreateUnableToPublishException(Process process)
        {
            var processId = ProcessHelper.GetId(process);
            var exitCode = ProcessHelper.GetExitCode(process);
            return new ProcessShardException(
                $"Unable to publish messages because publish was cancelled waiting for process to start. Last time process {processId} stopped with exit code {exitCode}.",
                Id, processId, exitCode);
        }

        private async Task<ProcessAndHandler> GetRunningProcessAsync(CancellationToken ct)
        {
            while (true)
            {
                var process = _current;
                if (process != null && ProcessHelper.IsAlive(process.Process) && process.Handler.IsReady())
                    return process;
                try
                {
                    await Task.Delay(_options.ProcessStatusCheckInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    if (process != null) throw CreateUnableToPublishException(process.Process);
                    throw;
                }
            }
        }

        private void OnCancellation(Process process)
        {
            var processId = ProcessHelper.GetId(process);
            using var logScope = BeginProcessLoggingScope(process);
            try
            {
                if (ProcessHelper.IsDead(process))
                    return;

                if (_options.GracefulStopTimeout != null)
                    if (process.WaitForExit((int)Math.Ceiling(_options.GracefulStopTimeout.Value.TotalMilliseconds)))
                        return;

                process.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Cancellation failed on process {process_id} shard", processId);
            }
        }

        private async Task ProcessOutputs<T>(
            Func<CancellationToken, IAsyncEnumerable<T>> enumerationProvider,
            Channel<ShardMessage<T>> output,
            int? processId,
            CancellationToken ct)
        {
            await Task.Yield();
            while (!ct.IsCancellationRequested)
                try
                {
                    await foreach (var item in enumerationProvider(ct)
                                       .ConfigureAwait(false))
                        await output.Writer.WriteAsync(new ShardMessage<T>
                        {
                            Timestamp = DateTime.UtcNow,
                            Value = item
                        }, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException ode) when (ode.ObjectName == Id)
                {
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to retrieve outputs/errors from process {process_id} shard",
                        processId);
                }
        }

        private async Task WaitUntilExit(Process process)
        {
            while (ProcessHelper.IsAlive(process)) await Task.Delay(_options.ProcessStatusCheckInterval).ConfigureAwait(false);
        }

        private async Task WaitUntilReady(IProcessStateProvider provider, CancellationToken ct)
        {
            while (!provider.IsReady()) await Task.Delay(_options.ProcessStatusCheckInterval, ct).ConfigureAwait(false);
        }

        public ChannelReader<ShardMessage<TError>> Errors => _errors.Reader;

        public ChannelReader<ShardMessage<TOutput>> Outputs => _outputs.Reader;

        public string Id { get; }
        private readonly Channel<ShardMessage<TError>> _errors;
        private readonly Channel<ShardMessage<TOutput>> _outputs;
        private readonly IChildProcessWatcher _watcher;
        private readonly ILogger _logger;
        private readonly ProcessShardOptions _options;
        private readonly IProcessInputOutputHandlerFactory<TInput, TOutput, TError> _handlerFactory;
        private readonly SemaphoreSlim _publishSemaphore;
        private bool _disposed;
        private ProcessAndHandler _current;

        private class ProcessAndHandler : IDisposable
        {
            private readonly Process _process;
            private readonly IProcessInputOutputHandler<TInput, TOutput, TError> _handler;
            public Process Process => _process;
            public IProcessInputOutputHandler<TInput, TOutput, TError> Handler => _handler;

            public ProcessAndHandler(Process process, IProcessInputOutputHandler<TInput, TOutput, TError> handler)
            {
                _process = process;
                _handler = handler;
            }

            public void Dispose()
            {
                try
                {
                    Process.Dispose();
                }
                catch{}

                try
                {
                    Handler.Dispose();
                }
                catch{}
            }
        }
    }
}