using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Jobs
{
    public interface ILifetimeManager
    {
        Task StopAsync(CancellationToken ct);

        Task StartAsync(CancellationToken ct);

        Task RestartAsync(CancellationToken ct);
    }
}