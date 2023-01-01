using System;
using System.Collections.Generic;

namespace Eocron.Sharding.Messaging
{
    public class BrokerMessage<TMessage>
    {
        public string Key { get; set; }

        public TMessage Message { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return string.Format($"{Key}");
        }
    }
}