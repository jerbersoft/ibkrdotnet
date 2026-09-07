using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Reads the current time and waits, as one abstraction, so rate limiting can be tested without
/// real elapsed time.
/// </summary>
/// <remarks>
/// Splitting "what time is it" from "wait this long" across two abstractions lets them drift apart
/// under test, which is exactly the situation a rate limiter must not be verified in.
/// </remarks>
internal interface IDelayScheduler
{
    Instant Now { get; }

    Task DelayAsync(Duration duration, CancellationToken cancellationToken);
}

/// <summary>
/// The production scheduler: reads a NodaTime <see cref="IClock"/> and waits on the thread pool.
/// </summary>
internal sealed class SystemDelayScheduler(IClock clock) : IDelayScheduler
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public Instant Now => _clock.GetCurrentInstant();

    public Task DelayAsync(Duration duration, CancellationToken cancellationToken) =>
        duration <= Duration.Zero
            ? Task.CompletedTask
            : Task.Delay(duration.ToTimeSpan(), cancellationToken);
}
