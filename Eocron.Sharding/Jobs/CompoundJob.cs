using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Jobs
{
    public sealed class CompoundJob : IJob
    {
        public CompoundJob(params IJob[] jobs)
        {
            _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        }

        public void Dispose()
        {
            foreach (var inner in _jobs) inner.Dispose();
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await Task.Yield();
            await Task.WhenAll(_jobs.Select(x => x.RunAsync(ct))).ConfigureAwait(false);
        }

        private readonly IJob[] _jobs;
    }
}