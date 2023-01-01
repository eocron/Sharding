using System.Text;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding.Tests.Helpers;

public class TestLogger : ILogger
{
    private readonly string _name;

    public TestLogger(string name = default)
    {
        _name = name;
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var sb = new StringBuilder();
        if (_name != null)
        {
            sb.Append($"[{_name}]");
        }
        sb.Append($"[{logLevel}]: {formatter(state, exception)}");
        if (exception != null)
        {
            sb.Append(", error: " + exception.ToString());
        }
        Console.WriteLine(sb.ToString());
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }
}