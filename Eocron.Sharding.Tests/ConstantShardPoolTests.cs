using Eocron.Sharding.Pools;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    public sealed class ConstantShardPoolTests
    {
        private Mock<IShardFactory<string, string, string>> _shardFactory;
        private ConstantShardPool<string, string, string> _pool;
        private CancellationTokenSource _cts;
        private Task _task;
        private List<Mock<IShard<string, string, string>>> _shards;

        [SetUp]
        public async Task SetUp()
        {
            var logger = new TestLogger();
            _shards = new List<Mock<IShard<string, string, string>>>();
            _shardFactory = new Mock<IShardFactory<string, string, string>>();
            _shardFactory.Setup(x => x.CreateNewShard(It.IsAny<string>())).Returns<string>((id) =>
            {
                var shard = new Mock<IShard<string, string, string>>();
                shard.Setup(x => x.Id).Returns(id);
                shard.Setup(x => x.RunAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));
                _shards.Add(shard);
                return shard.Object;
            });
            _cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            _pool = new ConstantShardPool<string, string, string>(logger, _shardFactory.Object, 3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
            _task = _pool.RunAsync(_cts.Token);
            await Task.Delay(1);
        }

        [TearDown]
        public async Task TearDown()
        {
            _cts.Cancel();
            try
            {
                await _task;
            }
            catch(OperationCanceledException)
            {

            }
        }

        [Test]
        public void ReserveReturn()
        {
            var ids = _pool.GetAllShards().Select(x=> x.Id).ToList();
            var id = ids.First();
            id.Should().NotBeNullOrWhiteSpace();
            _pool.TryReserve(id, out var shard).Should().BeTrue();
            shard.Should().NotBeNull();
            var afterReserveIds = _pool.GetAllShards().Select(x => x.Id).ToList();
            afterReserveIds.Should().BeEquivalentTo(ids);
            var afterReserverShard = _pool.GetShard(shard.Id);
            afterReserverShard.Should().NotBeNull();
            afterReserverShard.Should().Be(shard);

            _pool.TryReserve(id, out var shard2).Should().BeFalse();
            shard2.Should().BeNull();
            _pool.Return(shard);
            _pool.TryReserve(id, out shard).Should().BeTrue();
            _pool.Return(shard);
        }
    }
}
