using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Tests.Helpers
{
    public class InMemoryConsumerFactory<TKey, TMessage> : IBrokerConsumerFactory<TMessage>
    {
        private List<BrokerMessage<TMessage>> _queue;
        private int _batchSize;
        private int _currentPosition;

        public InMemoryConsumerFactory(int batchSize, List<BrokerMessage<TMessage>> queue)
        {
            _batchSize = batchSize;
            _queue = queue;
        }

        public IBrokerConsumer<TMessage> CreateConsumer()
        {
            return new InMemoryConsumer<TMessage>(_currentPosition, _queue, _batchSize, x => _currentPosition = x);
        }
    }
}