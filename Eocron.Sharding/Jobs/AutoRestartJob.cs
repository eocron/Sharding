using System.Threading;
using System.Threading.Tasks;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding.Jobs
{
    public sealed class AutoRestartJob : IJob
    {
        private readonly ILifetimeManager _manager;
        private readonly IShardStateProvider _stateProvider;
        private readonly bool _skipFirst;
        private readonly bool _forceRestartOnBusy;
        private bool _skipped;

        public AutoRestartJob(ILifetimeManager manager, IShardStateProvider stateProvider, bool skipFirst, bool forceRestartOnBusy)
        {
            _manager = manager;
            _stateProvider = stateProvider;
            _skipFirst = skipFirst;
            _forceRestartOnBusy = forceRestartOnBusy;
        }

        public void Dispose()
        {
            
        }

        public async Task RunAsync(CancellationToken ct)
        {
            if (_skipFirst && !_skipped)
            {
                _skipped = true;
                return;
            }

            if (!_forceRestartOnBusy)
            {
                await _stateProvider.WhenReady(ct).ConfigureAwait(false);
            }
            await _manager.RestartAsync(ct).ConfigureAwait(false);
        }
    }
}