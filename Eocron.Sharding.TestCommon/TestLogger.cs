using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.TestCommon;

public class TestLogger : ILogger
{
    private readonly string _name;
    private readonly LogLevel _minLogLevel;

    public TestLogger(string name = default, LogLevel minLogLevel = LogLevel.Debug)
    {
        _name = name;
        _minLogLevel = minLogLevel;
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if(!IsEnabled(logLevel))
            return;

        var sb = new StringBuilder();
        sb.Append($"[{DateTime.UtcNow.ToString("T")}]");
        if (_name != null)
        {
            sb.Append($"[{_name}]");
        }
        sb.Append($"[{logLevel}]: {formatter(state, exception)}");
        if (exception != null)
        {
            sb.Append(", error: " + exception.ToString());
        }

        Task.Run(() => Console.WriteLine(sb.ToString()));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _minLogLevel <= logLevel;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }
}