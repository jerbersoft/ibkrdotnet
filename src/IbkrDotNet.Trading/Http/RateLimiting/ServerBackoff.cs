using System.Collections.Concurrent;
using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Holds requests back after Interactive Brokers has answered one with <c>429 Too Many Requests</c>,
/// for as long as that response asked for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SlidingWindowLimiter"/> paces against the limits this library believes IBKR enforces.
/// Parts of that table are inference rather than publication -- <c>/pa/allperiods</c> appears in no
/// IBKR limits table at all and is paced with its neighbours -- so the belief can be wrong, and a
/// <c>429</c> is the only evidence that it is. This records the evidence: the next request that
/// would have gone to the same place waits out the window IBKR named before it is let through,
/// instead of arriving into a limit that has already rejected it once.
/// </para>
/// <para>
/// The hold is deliberately temporary. A <c>429</c> can equally come from a second process on the
/// same username, an address shared with someone else's client, or something transient at IBKR's
/// end, none of which mean the table is wrong -- so the window expires and pacing returns to normal,
/// rather than one bad response permanently halving the throughput of a healthy client.
/// </para>
/// <para>
/// Keys are opaque to this class. <see cref="IbkrRateLimiterRegistry"/> supplies the name of the
/// limiter that paced the request, so the hold covers everything sharing that limit, and falls back
/// to the request path when no limit matched -- which is the case the feedback exists for.
/// </para>
/// </remarks>
internal sealed class ServerBackoff(IDelayScheduler scheduler)
{
    private readonly ConcurrentDictionary<string, Instant> _heldUntil = new(StringComparer.Ordinal);

    /// <summary>The number of holds outstanding. Exposed so that pruning can be asserted.</summary>
    public int Count => _heldUntil.Count;

    /// <summary>
    /// Records a rejection against a key, and returns how long the key is now held for.
    /// </summary>
    /// <param name="key">The key to hold.</param>
    /// <param name="backoff">How long to hold it.</param>
    /// <returns>
    /// The remaining hold, which is longer than <paramref name="backoff"/> when an earlier rejection
    /// is still in force. Holds extend but never shorten: two rejections in flight at once must not
    /// let the second, milder one release the first.
    /// </returns>
    public Duration Record(string key, Duration backoff)
    {
        var now = scheduler.Now;
        var until = now + (backoff > Duration.Zero ? backoff : Duration.Zero);
        var held = _heldUntil.AddOrUpdate(
            key,
            until,
            (_, existing) => existing > until ? existing : until);

        Prune(now);
        return held - now;
    }

    /// <summary>Waits until a key is clear.</summary>
    /// <param name="key">The key to wait on.</param>
    /// <param name="maxWait">
    /// The longest the caller is willing to wait, after which the hold is reported rather than
    /// taken -- the same threshold local pacing applies, for the same reason.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task WaitAsync(string key, Duration maxWait, CancellationToken cancellationToken)
    {
        // The common case by a wide margin: nothing has been rejected, so there is nothing to check.
        if (_heldUntil.IsEmpty)
        {
            return;
        }

        var waited = Duration.Zero;
        while (_heldUntil.TryGetValue(key, out var until))
        {
            var remaining = until - scheduler.Now;
            if (remaining <= Duration.Zero)
            {
                // Removed by value, so a hold extended by a concurrent rejection is not dropped.
                _heldUntil.TryRemove(new KeyValuePair<string, Instant>(key, until));
                return;
            }

            if (waited + remaining > maxWait)
            {
                throw new IbkrRateLimitExceededException(
                    $"IBKR rejected a request to '{key}' with 429 Too Many Requests, and the client " +
                    $"is holding that endpoint for another {remaining}. That is longer than the " +
                    $"configured maximum wait of {maxWait}, so the request was not sent.")
                {
                    Limit = key,
                    RetryAfter = remaining,
                };
            }

            await scheduler.DelayAsync(remaining, cancellationToken).ConfigureAwait(false);
            waited += remaining;

            // Looping rather than returning: a rejection that landed while this caller was waiting
            // may have pushed the hold out past the point it just waited to.
        }
    }

    /// <summary>
    /// Drops holds that have elapsed, so that a client rejected on many distinct paths does not
    /// accumulate an entry per path for the life of the process.
    /// </summary>
    private void Prune(Instant now)
    {
        foreach (var held in _heldUntil)
        {
            if (held.Value <= now)
            {
                _heldUntil.TryRemove(held);
            }
        }
    }
}
