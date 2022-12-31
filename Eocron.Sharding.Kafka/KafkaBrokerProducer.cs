using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Eocron.Sharding.Kafka
{
    public sealed class KafkaBrokerProducer<TKey, TMessage> : IBrokerProducer<TKey, TMessage>
    {
        private readonly string _topicName;
        private readonly Lazy<IProducer<TKey, TMessage>> _producer;
        private readonly CancellationTokenSource _cts;

        public KafkaBrokerProducer(ProducerBuilder<TKey, TMessage> builder, string topicName)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            _topicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
            _producer = new Lazy<IProducer<TKey, TMessage>>(builder.Build);
            _cts = new CancellationTokenSource();
        }

        public async Task PublishAsync(IEnumerable<BrokerMessage<TKey, TMessage>> messages, CancellationToken ct)
        {
            foreach (var brokerMessage in messages)
            {
                var headers = new Headers();
                if (brokerMessage.Headers != null)
                {
                    foreach (var brokerMessageHeader in brokerMessage.Headers)
                    {
                        headers.Add(brokerMessageHeader.Key, Encoding.UTF8.GetBytes(brokerMessageHeader.Value));
                    }
                }
                await _producer
                    .Value
                    .ProduceAsync(
                        _topicName,
                        new Message<TKey, TMessage>()
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

        public void Dispose()
        {
            _cts.Cancel();
            if (_producer.IsValueCreated)
            {
                _producer.Value.Dispose();
            }
            _cts.Dispose();
        }
    }
}