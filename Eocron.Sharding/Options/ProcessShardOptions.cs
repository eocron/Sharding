using System;
using System.Diagnostics;
using System.Threading.Channels;

namespace Eocron.Sharding.Options
{
    public class ProcessShardOptions
    {
        public bool EnrichHeaders { get; set; }
        public BoundedChannelOptions ErrorOptions { get; set; }

        public BoundedChannelOptions OutputOptions { get; set; }

        public ProcessStartInfo StartInfo { get; set; }

        /// <summary>
        ///     How much time to wait on graceful shutdown before process forcefully killed.
        ///     If not set process killed immediately.
        /// </summary>
        public TimeSpan? GracefulStopTimeout { get; set; }

        public RestartPolicyOptions RestartPolicy { get; set; }

        public ProcessShardOptions()
        {
            EnrichHeaders = true;
            ErrorOptions = new(10000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            };
            OutputOptions = new(10000)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            RestartPolicy = RestartPolicyOptions.Constant(TimeSpan.FromSeconds(1));
        }
    }
}