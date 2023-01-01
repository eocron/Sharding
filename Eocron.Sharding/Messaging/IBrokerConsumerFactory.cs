namespace Eocron.Sharding.Messaging
{
    public interface IBrokerConsumerFactory<TKey, TMessage>
    {
        IBrokerConsumer<TKey, TMessage> CreateConsumer();
    }
}