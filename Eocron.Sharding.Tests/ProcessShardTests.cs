using Eocron.Sharding.Messaging;
using Eocron.Sharding.Tests.Helpers;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    [TestFixture]
    public class ProcessShardTests
    {
        [Test]
        public async Task CantStart()
        {
            using var cts = new CancellationTokenSource(TestTimeout);
            var handle = new Mock<ProcessShardHelper.ITestProcessJobHandle>();
            using var shard = ProcessShardHelper.CreateTestShard("ErrorImmediately", handle.Object);
            var task = shard.RunAsync(cts.Token);
            var publish = shard.PublishAsync(new[] { "a", "b", "c" }.Select(x=> new BrokerMessage<string>(){Message = x}), cts.Token);
            await handle.VerifyForever(x => x.OnStarting(), Times.AtLeast(3), cts.Token);
            cts.Cancel();
            await task;
            handle.Verify(x => x.OnStopped(), Times.AtMost(1));
        }

        [Test,Explicit]
        public async Task CantStartProfile()
        {
            for (int i = 0; i < 100; i++)
            {
                await CantStart().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Hang()
        {
            var cts = new CancellationTokenSource(TestTimeout);
            using var shard = ProcessShardHelper.CreateTestShard("stream");
            var task = shard.RunAsync(cts.Token);
            var outputs = new List<BrokerMessage<string>>();
            await shard.PublishAndHandleUntilReadyAsync(new[] { "hang", "test" }.AsTestMessages(),
                async (x, ct) => outputs.AddRange(x),
                (x, ct) => Task.CompletedTask,
                cts.Token);
            cts.Cancel();

            await task;

            outputs.Select(x => x.Message).Should().BeEquivalentTo(new[] { "hang" });
        }

        [Test]
        public async Task HangRestart()
        {
            var cts = new CancellationTokenSource(TestTimeout);
            using var shard = ProcessShardHelper.CreateTestShard("stream");
            var task = shard.RunAsync(cts.Token);
            await shard.PublishAsync(new[] { "hang" }.AsTestMessages(), cts.Token);
            await Task.Delay(100);
            await shard.RestartAsync(cts.Token);
            await shard.PublishAsync(new[] { "test" }.AsTestMessages(), cts.Token);
            await Task.Delay(100);
            cts.Cancel();

            await task;

            await ProcessShardHelper.AssertErrorsAndOutputs(
                shard,
                new string[] { "hang", "test" },
                Array.Empty<string>(),
                CancellationToken.None,
                TimeSpan.FromSeconds(1));
        }

        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    }
}
