using System;

namespace Eocron.Sharding.Options
{
    public class RestartPolicyOptions
    {
        public TimeSpan OnSuccessDelay { get; set; }

        public TimeSpan OnErrorDelay { get; set; }


        public static RestartPolicyOptions Constant(TimeSpan delay)
        {
            return new RestartPolicyOptions
            {
                OnSuccessDelay = delay,
                OnErrorDelay = delay
            };
        }

        public RestartPolicyOptions()
        {
            OnSuccessDelay = TimeSpan.FromSeconds(1);
            OnErrorDelay = TimeSpan.FromSeconds(1);
        }
    }
}