using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Processing
{
    public interface IShardLifetimeProvider
    {
        Task<bool> IsStoppedAsync(CancellationToken ct);
    }
}