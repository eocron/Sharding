using System.Diagnostics;

namespace Eocron.Sharding.Handlers
{
    public interface IProcessInputOutputHandlerFactory<TInput, TOutput, TError>
    {
        IProcessInputOutputHandler<TInput, TOutput, TError> CreateHandler(Process process);
    }
}