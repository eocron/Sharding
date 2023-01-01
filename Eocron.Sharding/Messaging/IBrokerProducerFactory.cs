namespace Eocron.Sharding.Messaging
{
    public interface IBrokerProducerFactory
    {
        IBrokerProducer<TMessage> CreateProducer<TMessage>();
    }
}