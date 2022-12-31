namespace Eocron.Sharding.Kafka
{
    public interface IBrokerConsumerFactory
    {
        IBrokerConsumer<TKey, TMessage> CreateConsumer<TKey, TMessage>();
    }
}