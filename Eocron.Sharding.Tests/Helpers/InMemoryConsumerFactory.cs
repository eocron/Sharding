using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Tests.Helpers
{
    public class InMemoryConsumerFactory<TKey, TMessage> : IBrokerConsumerFactory<TKey, TMessage>
    {
        private List<BrokerMessage<TKey, TMessage>> _queue;
        private int _batchSize;
        private int _currentPosition;

        public InMemoryConsumerFactory(int batchSize, List<BrokerMessage<TKey, TMessage>> queue)
        {
            _batchSize = batchSize;
            _queue = queue;
        }

        public IBrokerConsumer<TKey, TMessage> CreateConsumer()
        {
            return new InMemoryConsumer<TKey, TMessage>(_currentPosition, _queue, _batchSize, x => _currentPosition = x);
        }
    }
}