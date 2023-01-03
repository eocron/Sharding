using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Helpers
{
    public static class DelayHelper
    {
        public const double GoldenRatio = 1.61803398874989484820458683436;

        /// <summary>
        /// Wait until condition is true.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="ct"></param>
        /// <param name="delayProvider">By default this function checks condition with exponentially increasing delay. The best base was calculated to be GoldenRation and delay capped at 5 seconds,
        /// other bases give chaotic losses in time but can improve check performance. 5 second cap used solely because checks considered to be fast. For longer operations its better to use longer cap.</param>
        /// <returns></returns>
        public static async Task WhileTrueAsync(Func<Task<bool>> condition, CancellationToken ct, Func<int, TimeSpan> delayProvider = null)
        {
            delayProvider ??= x => ExponentialDelayPolicy(x, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(5));
            for (var i = 0; ; i++)
            {
                if (!await condition().ConfigureAwait(false))
                {
                    break;
                }

                var delayMs = delayProvider(i).Ticks / TimeSpan.TicksPerMillisecond;
                await Task.Delay((int)delayMs, ct).ConfigureAwait(false);
            }
        }

        public static TimeSpan ExponentialDelayPolicy(int index, TimeSpan minDelay, TimeSpan maxDelay, double @base = GoldenRatio)
        {
            var maxIndex = (int)Math.Ceiling(Math.Log(maxDelay.Ticks / (double)minDelay.Ticks, @base));
            var delay = TimeSpan.FromTicks(index < maxIndex
                ? (long)Math.Ceiling(Math.Min(minDelay.Ticks * Math.Pow(@base, index), maxDelay.Ticks))
                : maxDelay.Ticks);
            return delay;
        }

        public static TimeSpan ExponentialBase2DelayPolicy(int index, TimeSpan minDelay, TimeSpan maxDelay)
        {
            var maxIndex = (int)Math.Ceiling(Math.Log(maxDelay.Ticks / (double)minDelay.Ticks, 2));
            var delay = TimeSpan.FromTicks(index < maxIndex
                ? Math.Min(minDelay.Ticks << index, maxDelay.Ticks)
                : maxDelay.Ticks);
            return delay;
        }

        public static TimeSpan LinearDelayPolicy(int index, TimeSpan minDelay, TimeSpan maxDelay, int steps)
        {
            var delta = (maxDelay.Ticks - minDelay.Ticks) / steps;
            var delay = TimeSpan.FromTicks(Math.Min(minDelay.Ticks + delta * index, maxDelay.Ticks));
            return delay;
        }

        public static TimeSpan ConstantDelayPolicy(int index, TimeSpan delay)
        {
            return delay;
        }
    }
}