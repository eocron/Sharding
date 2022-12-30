using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Jobs
{
    public interface ILifetimeProvider
    {
        Task<bool> IsStoppedAsync(CancellationToken ct);
    }
}