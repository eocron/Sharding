using Moq;
using System.Linq.Expressions;

namespace Eocron.Sharding.Tests.Helpers
{
    public static class MockHelper
    {
        public static async Task VerifyForever<T, TResult>(this Mock<T> mock, Expression<Func<T, TResult>> expression, Times times, CancellationToken ct) where T : class
        {
            while (true)
            {
                try
                {
                    mock.Verify(expression, times);
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

        public static async Task VerifyForever<T>(this Mock<T> mock, Expression<Action<T>> expression, Times times, CancellationToken ct) where T : class
        {
            while (true)
            {
                try
                {
                    mock.Verify(expression, times);
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
    }
}
