using System.Diagnostics;

namespace Eocron.Sharding.Configuration
{
    public interface IProcessInputOutputHandlerFactory<TInput, TOutput, TError>
    {
        IProcessInputOutputHandler<TInput, TOutput, TError> CreateHandler(Process process);
    }
}