using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Processing
{
    public interface IShardLifetimeManager
    {
        Task StopAsync(CancellationToken ct);

        Task StartAsync(CancellationToken ct);

        Task RestartAsync(CancellationToken ct);
    }
}