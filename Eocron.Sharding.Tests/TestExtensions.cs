using Eocron.Sharding.Messaging;

namespace Eocron.Sharding.Tests
{
    public static class TestExtensions
    {
        public static async Task RetryForever(Action action, CancellationToken ct)
        {
            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch
                {
                    if (ct.IsCancellationRequested)
                        throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }
        public static IEnumerable<BrokerMessage<T>> AsTestMessages<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.Select(x => new BrokerMessage<T>()
            {
                Key = Guid.NewGuid().ToString(),
                Message = x,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
