using IbkrDotNet.Trading.Http.RateLimiting;
using NodaTime;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// A delay scheduler that advances a virtual clock instead of waiting, and records what it was
/// asked to wait for.
/// </summary>
public sealed class FakeDelayScheduler : IDelayScheduler
{
    private readonly List<Duration> _delays = [];
    private Action<Duration>? _onDelay;

    public FakeDelayScheduler(Instant? start = null)
    {
        Now = start ?? Instant.FromUtc(2024, 1, 1, 0, 0);
    }

    public Instant Now { get; private set; }

    public IReadOnlyList<Duration> Delays => _delays;

    public Duration TotalDelay => _delays.Aggregate(Duration.Zero, (sum, d) => sum + d);

    /// <summary>
    /// Runs a callback at the moment each wait begins, before the clock moves, so a test can stage
    /// something that happens while a caller is waiting.
    /// </summary>
    public void OnDelay(Action<Duration> onDelay) => _onDelay = onDelay;

    public Task DelayAsync(Duration duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _onDelay?.Invoke(duration);
        _delays.Add(duration);
        Now += duration;
        return Task.CompletedTask;
    }

    public void Advance(Duration duration) => Now += duration;
}
