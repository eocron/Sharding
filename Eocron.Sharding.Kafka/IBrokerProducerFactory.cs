namespace Eocron.Sharding.Kafka
{
    public interface IBrokerProducerFactory
    {
        IBrokerProducer<TKey, TMessage> CreateProducer<TKey, TMessage>();
    }
}