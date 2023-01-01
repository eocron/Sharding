namespace Eocron.Sharding.Kafka
{
    public interface IBrokerConsumerFactory<TKey, TMessage>
    {
        IBrokerConsumer<TKey, TMessage> CreateConsumer();
    }
}