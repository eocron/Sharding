using System;
using Eocron.Sharding.Helpers;

namespace Eocron.Sharding.Options
{
    public class RestartPolicyOptions
    {
        public Func<int, TimeSpan> OnSuccessDelay { get; private set; }

        public Func<int, TimeSpan> OnErrorDelay { get; private set; }

        public static RestartPolicyOptions Custom(Func<int, TimeSpan> onSuccess, Func<int, TimeSpan> onError)
        {
            return new RestartPolicyOptions()
            {
                OnErrorDelay = onError,
                OnSuccessDelay = onSuccess
            };
        }

        public static RestartPolicyOptions Constant(TimeSpan delay)
        {
            return new RestartPolicyOptions
            {
                OnSuccessDelay = x=> DelayHelper.ConstantDelayPolicy(x, delay),
                OnErrorDelay = x => DelayHelper.ConstantDelayPolicy(x, delay),
            };
        }

        public static RestartPolicyOptions Constant(TimeSpan onSuccessDelay, TimeSpan onErrorDelay)
        {
            return new RestartPolicyOptions
            {
                OnSuccessDelay = x => DelayHelper.ConstantDelayPolicy(x, onSuccessDelay),
                OnErrorDelay = x => DelayHelper.ConstantDelayPolicy(x, onErrorDelay),
            };
        }

        private RestartPolicyOptions()
        {
        }
    }
}