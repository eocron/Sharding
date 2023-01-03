using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.TestCommon
{
    public class TestLoggerFactory : ILoggerFactory
    {
        private LogLevel _minLogLevel;

        public TestLoggerFactory(LogLevel minLogLevel = LogLevel.Debug)
        {
            _minLogLevel = minLogLevel;
        }

        public void Dispose()
        {
        
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, _minLogLevel);
        }

        public void AddProvider(ILoggerProvider provider)
        {
        
        }
    }
}