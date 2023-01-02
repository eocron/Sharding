using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding
{
    public static class TaskHelper
    {
        public static async Task WhileTrueAsync(Func<Task<bool>> condition, CancellationToken ct, TimeSpan? minDelay = null, TimeSpan? maxDelay = null, int steps = 10)
        {
            minDelay ??= TimeSpan.FromMilliseconds(1);
            maxDelay ??= TimeSpan.FromSeconds(5);

            var delta = (maxDelay.Value.Ticks - minDelay.Value.Ticks) / steps;
            for (var i = 0; ; i++)
            {
                if (!await condition().ConfigureAwait(false))
                {
                    break;
                }

                var delayMs = Math.Min(minDelay.Value.Ticks + delta * i, maxDelay.Value.Ticks) / TimeSpan.TicksPerMillisecond;
                await Task.Delay((int)delayMs, ct).ConfigureAwait(false);
            }
        }
    }
}