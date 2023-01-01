using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Kafka
{
    public sealed class KafkaBrokerProducer<TKey, TMessage> : IBrokerProducer<TMessage>
    {
        public KafkaBrokerProducer(ProducerBuilder<string, TMessage> builder, string topicName)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            _topicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
            _producer = new Lazy<IProducer<string, TMessage>>(builder.Build);
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_producer.IsValueCreated) _producer.Value.Dispose();
            _cts.Dispose();
        }

        public async Task PublishAsync(IEnumerable<BrokerMessage<TMessage>> messages, CancellationToken ct)
        {
            foreach (var brokerMessage in messages)
            {
                var headers = new Headers();
                if (brokerMessage.Headers != null)
                    foreach (var brokerMessageHeader in brokerMessage.Headers)
                        headers.Add(brokerMessageHeader.Key, Encoding.UTF8.GetBytes(brokerMessageHeader.Value));
                await _producer
                    .Value
                    .ProduceAsync(
                        _topicName,
                        new Message<string, TMessage>
                        {
                            Key = brokerMessage.Key,
                            Value = brokerMessage.Message,
                            Headers = headers,
                            Timestamp = new Timestamp(brokerMessage.Timestamp)
                        },
                        ct)
                    .ConfigureAwait(false);
            }
        }

        private readonly CancellationTokenSource _cts;
        private readonly Lazy<IProducer<string, TMessage>> _producer;
        private readonly string _topicName;
    }
}