using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Admits at most <c>permits</c> requests in any <c>window</c>, delaying callers that arrive early.
/// </summary>
/// <remarks>
/// Callers are admitted in arrival order: the gate is held across the wait, so a burst is paced out
/// rather than released all at once when the window rolls.
/// </remarks>
internal sealed class SlidingWindowLimiter(
    string name,
    int permits,
    Duration window,
    IDelayScheduler scheduler) : IDisposable
{
    private readonly Queue<Instant> _admitted = new(permits);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Name { get; } = name;

    public int Permits { get; } = permits > 0
        ? permits
        : throw new ArgumentOutOfRangeException(nameof(permits), permits, "Permits must be positive.");

    public Duration Window { get; } = window > Duration.Zero
        ? window
        : throw new ArgumentOutOfRangeException(nameof(window), window, "The window must be positive.");

    /// <summary>
    /// Waits until a permit is available, then consumes it.
    /// </summary>
    /// <param name="maxWait">
    /// The longest the caller is willing to wait. When the required wait is longer, an
    /// <see cref="IbkrRateLimitExceededException"/> is thrown instead: silently blocking for the
    /// fifteen minutes some IBKR endpoints require would look like a hang.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task AcquireAsync(Duration maxWait, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = scheduler.Now;
            Trim(now);

            if (_admitted.Count >= Permits)
            {
                var wait = _admitted.Peek() + Window - now;
                if (wait > maxWait)
                {
                    throw new IbkrRateLimitExceededException(
                        $"Sending this request would exceed the '{Name}' limit of {Permits} request(s) " +
                        $"per {Window}. The next permit is available in {wait}, which is longer than " +
                        $"the configured maximum wait of {maxWait}.")
                    {
                        Limit = Name,
                        RetryAfter = wait,
                    };
                }

                if (wait > Duration.Zero)
                {
                    await scheduler.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
                    now = scheduler.Now;
                    Trim(now);
                }
            }

            _admitted.Enqueue(now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private void Trim(Instant now)
    {
        var cutoff = now - Window;
        while (_admitted.Count > 0 && _admitted.Peek() <= cutoff)
        {
            _admitted.Dequeue();
        }
    }
}
