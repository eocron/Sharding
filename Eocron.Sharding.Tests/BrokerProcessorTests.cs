using Eocron.Sharding.Jobs;
using Eocron.Sharding.Messaging;
using Eocron.Sharding.Pools;
using Eocron.Sharding.Tests.Helpers;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    public class BrokerProcessorTests
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            var logger = new TestLogger();
            _cts = new CancellationTokenSource(TestTimeout);
            _shardFactory = ProcessShardHelper.CreateTestShardFactory("stream");
            _pool = new ConstantShardPool<string, string, string>(
                logger, _shardFactory, 1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            _consumer = new Mock<IBrokerConsumer<string>>();
            _consumer.Setup(x => x.GetConsumerAsyncEnumerable(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(ct=> AsyncEnumerable.Empty<IEnumerable<BrokerMessage<string>>>());
            _consumer.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _producer = new Mock<IBrokerProducer<string>>();
            _cf = new Mock<IBrokerConsumerFactory<string>>();
            _cf.Setup(x => x.CreateConsumer()).Returns(_consumer.Object);
            _pf = new Mock<IBrokerProducerFactory>();
            _pf.Setup(x => x.CreateProducer<string>()).Returns(_producer.Object);
            _job =
                new CompoundJob(
                    new RestartUntilCancelledJob(
                        _pool,
                        new TestLogger("restart_pool"),
                        TimeSpan.Zero, 
                        TimeSpan.Zero),
                    new RestartUntilCancelledJob(
                        new BrokerShardProcessorJob<string, string, string>(
                            _cf.Object,
                            _pf.Object,
                            _pf.Object,
                            _pool,
                             new TestLogger("processor")
                        ),
                        new TestLogger("restart_processor"),
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(1)));
            _task = _job.RunAsync(_cts.Token);
        }

        [OneTimeTearDown]
        public async Task Teardown()
        {
            _cts.Cancel();
            await _task.ConfigureAwait(false);
            _job.Dispose();
        }

        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
        private CancellationTokenSource _cts;
        private Task _task;
        private Mock<IBrokerConsumer<string>> _consumer;
        private Mock<IBrokerProducer<string>> _producer;
        private Mock<IBrokerConsumerFactory<string>> _cf;
        private Mock<IBrokerProducerFactory> _pf;
        private IJob _job;
        private IShardFactory<string, string, string> _shardFactory;
        private ConstantShardPool<string, string, string> _pool;


        [Test]
        public async Task Publish()
        {
            var messages = CreateTestBrokerMessages(101).ToList();
            var factory = new InMemoryConsumerFactory<string, string>(10, messages);
            var consumer = factory.CreateConsumer();
            var actual = new List<BrokerMessage<string>>();
            _producer
                .Setup(x => x.PublishAsync(It.IsAny<IEnumerable<BrokerMessage<string>>>(),
                    It.IsAny<CancellationToken>())).Callback<IEnumerable<BrokerMessage<string>>, CancellationToken>(
                    (items, ct) =>
                    {
                        lock (actual)
                        {
                            actual.AddRange(items);
                        }
                    });
            _consumer.Reset();
            _consumer.Setup(x => x.GetConsumerAsyncEnumerable(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => consumer.GetConsumerAsyncEnumerable(ct));
            _consumer.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => consumer.CommitAsync(ct));

            await TestExtensions.RetryForever(() =>
            {
                actual.Select(x => x.Message).Should().BeEquivalentTo(messages.Select(x => x.Message));
            }, _cts.Token);
        }

        private static IEnumerable<BrokerMessage<string>> CreateTestBrokerMessages(int count)
        {
            return Enumerable.Range(0, count).Select(x => new BrokerMessage<string>()
            {
                Key = "in_"+(x+1),
                Message = "test_message_" + (x+1),
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
