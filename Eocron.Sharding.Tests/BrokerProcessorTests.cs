using System.Collections.Generic;
using System.Threading.Channels;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Kafka;
using Eocron.Sharding.Pools;
using Eocron.Sharding.Tests.Helpers;
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
            _consumer = new Mock<IBrokerConsumer<string, string>>();
            var messages = Channel.CreateUnbounded<BrokerMessage<string, string>>();
            _consumer.Setup(x => x.GetConsumerAsyncEnumerable(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(ct=> AsyncEnumerable.Empty<IEnumerable<BrokerMessage<string, string>>>());
            _consumer.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _producer = new Mock<IBrokerProducer<string, string>>();
            _cf = new Mock<IBrokerConsumerFactory<string, string>>();
            _cf.Setup(x => x.CreateConsumer()).Returns(_consumer.Object);
            _pf = new Mock<IBrokerProducerFactory>();
            _pf.Setup(x => x.CreateProducer<string, string>()).Returns(_producer.Object);
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

        public static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);
        private CancellationTokenSource _cts;
        private Task _task;
        private Mock<IBrokerConsumer<string, string>> _consumer;
        private Mock<IBrokerProducer<string, string>> _producer;
        private Mock<IBrokerConsumerFactory<string, string>> _cf;
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
            _consumer.Reset();
            _consumer.Setup(x => x.GetConsumerAsyncEnumerable(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => consumer.GetConsumerAsyncEnumerable(ct));
            _consumer.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => consumer.CommitAsync(ct));
            await _producer.VerifyForever(
                x => x.PublishAsync(It.IsAny<IEnumerable<BrokerMessage<string, string>>>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(11), _cts.Token);
        }


        private static IEnumerable<BrokerMessage<string, string>> CreateTestBrokerMessages(int count)
        {
            return Enumerable.Range(0, count).Select(x => new BrokerMessage<string, string>()
            {
                Key = Guid.NewGuid().ToString(),
                Message = "test_message_" + Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
