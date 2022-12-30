using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Configuration
{
    public interface IStreamWriterSerializer<in T>
    {
        Task SerializeToAsync(StreamWriter writer, IEnumerable<T> item, CancellationToken ct);
    }
}