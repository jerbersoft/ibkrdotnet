using Microsoft.Extensions.Logging;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// An <see cref="ILogger{T}"/> that keeps every entry at every level, so a test can assert on what
/// the code under test logged and with which exception.
/// </summary>
public sealed class FakeLogger<T> : ILogger<T>
{
    private readonly Lock _sync = new();
    private readonly List<Entry> _entries = [];

    /// <summary>Every entry logged so far, in order.</summary>
    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_sync)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var entry = new Entry(logLevel, eventId, formatter(state, exception), exception);
        lock (_sync)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>One logged entry.</summary>
    public sealed record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
