using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Tests.Helpers
{
    public class InMemoryConsumer<TMessage> : IBrokerConsumer<TMessage>
    {
        private int _currentPosition;
        private readonly List<BrokerMessage<TMessage>> _queue;
        private readonly int _batchSize;
        private readonly Action<int> _onCommit;
        private int _readPosition;

        public InMemoryConsumer(int currentPosition, List<BrokerMessage<TMessage>> queue, int batchSize, Action<int> onCommit)
        {
            _currentPosition = currentPosition;
            _queue = queue;
            _batchSize = batchSize;
            _onCommit = onCommit;
        }

        public void Dispose()
        {
            
        }

        public async IAsyncEnumerable<IEnumerable<BrokerMessage<TMessage>>> GetConsumerAsyncEnumerable(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _readPosition = _currentPosition;
            foreach (var i in _queue.Skip(_currentPosition).Chunk(_batchSize))
            {
                _readPosition += i.Length;
                yield return i;
            }
        }

        public async Task CommitAsync(CancellationToken ct)
        {
            _currentPosition = _readPosition;
            _onCommit(_currentPosition);
        }
    }
}
