using System.Diagnostics;
using System.Threading;
using Eocron.Sharding.Handlers;

namespace Eocron.Sharding.TestCommon
{
    public sealed class TestAppHandlerFactory : IProcessInputOutputHandlerFactory<string, string, string>
    {
        public static int _id;
        public string IdGenerator()
        {
            return Interlocked.Increment(ref _id).ToString();
        }
        public IProcessInputOutputHandler<string, string, string> CreateHandler(Process process)
        {
            return new TestAppHandler(process, IdGenerator);
        }
    }
}