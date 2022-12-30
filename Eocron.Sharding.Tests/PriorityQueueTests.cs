using Eocron.Sharding.DataStructures;
using FluentAssertions;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    public sealed class PriorityQueueTests
    {
        public IPriorityDictionary<int, int, int> _queue;
        [SetUp]
        public void Setup()
        {
            _queue = new PriorityDictionary<int, int, int>();
        }
        [Test]
        public void Enqueue()
        {
            _queue.Clear();
            _queue.Enqueue(1, 1, 1);
            _queue.Enqueue(2, 2, 2);
            _queue.Enqueue(3, 3, 3);

            _queue.TryDequeue(out var key, out var element).Should().BeTrue();
            key.Should().Be(1);
            element.Should().Be(1);

            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(2);
            element.Should().Be(2);

            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(3);
            element.Should().Be(3);

            _queue.TryDequeue(out key, out element).Should().BeFalse();
        }

        [Test]
        public void EnqueueDequeueEnqueue()
        {
            _queue.Clear();
            _queue.Enqueue(1, 1, 1);
            _queue.Enqueue(2, 2, 2);
            _queue.Enqueue(3, 3, 3);
            _queue.TryDequeue(out var key, out var element).Should().BeTrue();
            _queue.Enqueue(1, 1, 1);

            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(1);
            element.Should().Be(1);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(2);
            element.Should().Be(2);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(3);
            element.Should().Be(3);
            _queue.TryDequeue(out key, out element).Should().BeFalse();
        }

        [Test]
        public void EnqueueRemoveEnqueue()
        {
            _queue.Clear();
            _queue.Enqueue(1, 1, 1);
            _queue.Enqueue(2, 2, 2);
            _queue.Enqueue(3, 3, 3);
            _queue.TryRemoveByKey(2, out var element).Should().BeTrue();
            element.Should().Be(2);
            _queue.Enqueue(2, 2, 2);

            _queue.TryDequeue(out var key, out element).Should().BeTrue();
            key.Should().Be(1);
            element.Should().Be(1);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(2);
            element.Should().Be(2);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(3);
            element.Should().Be(3);
            _queue.TryDequeue(out key, out element).Should().BeFalse();
        }


        [Test]
        public void EnqueueUpdatePriority()
        {
            _queue.Clear();
            _queue.Enqueue(1, 1, 1);
            _queue.Enqueue(4, 2, 4);
            _queue.Enqueue(3, 3, 3);
            _queue.TryUpdatePriority(4, 4).Should().BeTrue();

            _queue.TryDequeue(out var key, out var element).Should().BeTrue();
            key.Should().Be(1);
            element.Should().Be(1);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(3);
            element.Should().Be(3);
            _queue.TryDequeue(out key, out element).Should().BeTrue();
            key.Should().Be(4);
            element.Should().Be(4);
            _queue.TryDequeue(out key, out element).Should().BeFalse();
        }
    }
}
