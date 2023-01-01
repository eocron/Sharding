namespace Eocron.Sharding.Handlers
{
    public interface IProcessStateProvider
    {
        /// <summary>
        ///     This method is for checking if process is healthy and ready to serve messages.
        ///     Called frequently.
        /// </summary>
        /// <returns>True - if process ready to process messages</returns>
        bool IsReady();
    }
}