using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.TestCommon
{
    public sealed class CooldownTask
    {
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _delay;
        private DateTime _deadline;

        public CooldownTask(TimeSpan timeout)
        {
            _timeout = timeout;
            _delay = TimeSpan.FromTicks(Math.Min(_timeout.Ticks >> 2, TimeSpan.TicksPerSecond));
        }

        public void Refresh()
        {
            _deadline = DateTime.UtcNow + _timeout;
        }
        public async Task WaitAsync(CancellationToken ct)
        {
            Refresh();
            while (_deadline > DateTime.UtcNow) await Task.Delay(_delay, ct).ConfigureAwait(false);
        }
    }
}