namespace Eocron.Sharding.Messaging
{
    public interface IBrokerConsumerFactory<TMessage>
    {
        IBrokerConsumer<TMessage> CreateConsumer();
    }
}