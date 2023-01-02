using System;

namespace Eocron.Sharding.Options
{
    public class ConstantShardPoolOptions
    {
        public int PoolSize { get; set; }

        public TimeSpan PriorityCheckInterval { get; set; }

        public TimeSpan PriorityCheckTimeout { get; set; }

        public ConstantShardPoolOptions()
        {
            PriorityCheckInterval = TimeSpan.FromSeconds(1);
            PriorityCheckTimeout = TimeSpan.FromSeconds(5);
        }
    }
}