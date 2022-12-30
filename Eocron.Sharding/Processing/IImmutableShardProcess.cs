namespace Eocron.Sharding.Processing
{
    public interface IImmutableShardProcess : IProcessDiagnosticInfoProvider, IShardStateProvider
    {
        string Id { get; }
    }
}