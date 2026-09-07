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

    public FakeDelayScheduler(Instant? start = null)
    {
        Now = start ?? Instant.FromUtc(2024, 1, 1, 0, 0);
    }

    public Instant Now { get; private set; }

    public IReadOnlyList<Duration> Delays => _delays;

    public Duration TotalDelay => _delays.Aggregate(Duration.Zero, (sum, d) => sum + d);

    public Task DelayAsync(Duration duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _delays.Add(duration);
        Now += duration;
        return Task.CompletedTask;
    }

    public void Advance(Duration duration) => Now += duration;
}
