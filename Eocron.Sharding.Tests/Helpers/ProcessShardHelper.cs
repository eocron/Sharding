using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Channels;
using Eocron.Sharding.Processing;
using App.Metrics;
using Eocron.Sharding.AppMetrics;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Messaging;
using Eocron.Sharding.TestCommon;
using Microsoft.Extensions.Logging;
using Eocron.Sharding.Options;

namespace Eocron.Sharding.Tests.Helpers
{
    public static class ProcessShardHelper
    {
        public static Task AssertErrorsAndOutputs<TInput, TOutput, TError>(
            IShard<TInput, TOutput, TError> shard,
            TOutput[] outputs, TError[] errors,
            CancellationToken ct,
            TimeSpan forTime)
        {
            return Task.WhenAll(
                AssertIsEqual(shard.Outputs.ReadAllAsync(ct), forTime, outputs),
                AssertIsEqual(shard.Errors.ReadAllAsync(ct), forTime, errors));
        }
        public static async Task AssertIsEqual<T>(IAsyncEnumerable<BrokerMessage<T>> enumerable, TimeSpan forTime, params T[] expected)
        {
            expected = expected ?? Array.Empty<T>();
            var result = await ConsumeFor(enumerable, forTime).ConfigureAwait(false);
            CollectionAssert.AreEqual(expected, result.Select(x => x.Message));
        }
        private static async Task<List<T>> ConsumeFor<T>(IAsyncEnumerable<T> enumerable, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            var result = new List<T>();
            try
            {
                await foreach (var i in enumerable.WithCancellation(cts.Token))
                {
                    result.Add(i);
                }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
            }

            return result;
        }

        public static IShardFactory<string, string, string> CreateTestShardFactory(string mode, ITestProcessJobHandle handle = null)
        {
            var watcher = new TestChildProcessWatcher();
            var metrics = new MetricsBuilder().Build();
            var factory = new TestAppHandlerFactory();
            return
                new ShardBuilder<string, string, string>()
                    .WithTransient<IChildProcessWatcher>(watcher)
                    .WithTransient<ILogger>(new TestLogger())
                    .WithTransient<IInputOutputHandlerFactory<string, string, string>>(factory)
                    .WithProcessJob(
                        new ProcessShardOptions
                        {
                            StartInfo = new ProcessStartInfo("Tools/Eocron.Sharding.TestApp.exe") { ArgumentList = { mode } }
                                .ConfigureAsService(),
                            RestartPolicy = RestartPolicyOptions.Constant(TimeSpan.FromMilliseconds(1))
                        })
                    .WithProcessJobWrap(x => new TestProcessJob<string, string, string>(x, handle))
                    .WithTransient<IMetrics>(metrics)
                    .WithAppMetrics(new AppMetricsShardOptions(){CheckInterval = TimeSpan.FromSeconds(1), CheckTimeout = TimeSpan.FromSeconds(5)})
                    .CreateFactory();
        }

        public static IShard<string, string, string> CreateTestShard(string mode, ITestProcessJobHandle handle = null)
        {
            return CreateTestShardFactory(mode, handle).CreateNewShard(nameof(ProcessShardTests) + Guid.NewGuid());
        }

        public interface ITestProcessJobHandle
        {
            void OnStarting();

            void OnStopped();
        }
        public class TestProcessJob<TInput, TOutput, TError> : IShardProcessJob<TInput, TOutput, TError>
        {
            private readonly IShardProcessJob<TInput, TOutput, TError> _processJobImplementation;
            private readonly ITestProcessJobHandle _handle;

            public TestProcessJob(IShardProcessJob<TInput, TOutput, TError> processJobImplementation, ITestProcessJobHandle handle)
            {
                _processJobImplementation = processJobImplementation;
                _handle = handle;
            }

            public string Id => _processJobImplementation.Id;

            public ChannelReader<BrokerMessage<TError>> Errors => _processJobImplementation.Errors;

            public ChannelReader<BrokerMessage<TOutput>> Outputs => _processJobImplementation.Outputs;

            public Task<bool> IsReadyAsync(CancellationToken ct)
            {
                return _processJobImplementation.IsReadyAsync(ct);
            }

            public Task PublishAsync(IEnumerable<BrokerMessage<TInput>> messages, CancellationToken ct)
            {
                return _processJobImplementation.PublishAsync(messages, ct);
            }

            public bool TryGetProcessDiagnosticInfo(out ProcessDiagnosticInfo info)
            {
                return _processJobImplementation.TryGetProcessDiagnosticInfo(out info);
            }

            public void Dispose()
            {
                _processJobImplementation.Dispose();
            }

            public async Task RunAsync(CancellationToken ct)
            {
                _handle?.OnStarting();
                await _processJobImplementation.RunAsync(ct).ConfigureAwait(false);
                _handle?.OnStopped();
            }
        }
    }
}
