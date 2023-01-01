namespace Eocron.Sharding.Messaging
{
    public interface IBrokerProducerFactory
    {
        IBrokerProducer<TKey, TMessage> CreateProducer<TKey, TMessage>();
    }
}