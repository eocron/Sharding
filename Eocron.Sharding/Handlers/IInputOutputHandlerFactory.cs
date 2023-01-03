using System.Diagnostics;

namespace Eocron.Sharding.Handlers
{
    public interface IInputOutputHandlerFactory<TInput, TOutput, TError>
    {
        IInputOutputHandler<TInput, TOutput, TError> CreateHandler(Process process);
    }
}