using System;
using System.Collections.Generic;

namespace Eocron.Sharding.Kafka
{
    public class BrokerMessage<TKey, TMessage>
    {
        public TKey Key { get; set; }

        public TMessage Message { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public DateTime Timestamp { get; set; }
    }
}