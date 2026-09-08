using System.Net.Http.Headers;
using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Holds the rate limiters and matches requests to them.
/// </summary>
/// <remarks>
/// The state lives here rather than in <see cref="IbkrRateLimitHandler"/> because
/// <c>IHttpClientFactory</c> rotates handler chains on a lifetime of its own — two minutes by
/// default — and building the limiters inside the handler would quietly reset every window on each
/// rotation. That is invisible against a one-second limit and completely wrong against the
/// fifteen-minute ones. Register this as a singleton.
/// </remarks>
public sealed class IbkrRateLimiterRegistry : IDisposable
{
    private readonly List<(PathTemplate Template, string? Method, SlidingWindowLimiter Limiter)> _endpointLimiters;
    private readonly IDelayScheduler _scheduler;
    private readonly ServerBackoff _backoff;
    private bool _disposed;

    /// <summary>Creates the registry from configured options.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="clock">The clock used to measure windows.</param>
    public IbkrRateLimiterRegistry(IOptions<IbkrTradingOptions> options, IClock clock)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)),
               new SystemDelayScheduler(clock))
    {
    }

    internal IbkrRateLimiterRegistry(IbkrTradingOptions options, IDelayScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scheduler);

        var limiting = options.RateLimiting;
        Enabled = limiting.Enabled;
        MaxWait = limiting.MaxWait;
        DefaultRetryAfter = limiting.DefaultRetryAfter;
        _scheduler = scheduler;
        _backoff = new ServerBackoff(scheduler);

        Global = limiting.EnforceGlobalLimit
            ? new SlidingWindowLimiter(
                "global",
                IbkrRateLimits.Global.Permits,
                IbkrRateLimits.Global.Window,
                scheduler)
            : null;

        _endpointLimiters = limiting.AdditionalLimits
            .Concat(IbkrRateLimits.PerEndpoint)
            .Select(limit => (
                Template: PathTemplate.Parse(limit.PathTemplate),
                limit.Method,
                Limiter: new SlidingWindowLimiter(
                    $"{limit.Method ?? "*"} {limit.PathTemplate}",
                    limit.Permits,
                    limit.Window,
                    scheduler)))
            .ToList();
    }

    /// <summary>Whether pacing is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>The longest a request will wait before the limit is reported as exceeded.</summary>
    public Duration MaxWait { get; }

    /// <summary>The hold applied to a rejection IBKR sent no <c>Retry-After</c> with.</summary>
    public Duration DefaultRetryAfter { get; }

    internal SlidingWindowLimiter? Global { get; }

    /// <summary>
    /// Waits until a request may be sent, taking a permit from every limit that applies to it.
    /// </summary>
    /// <remarks>
    /// This must be awaited outside the scope of <see cref="HttpClient.Timeout"/>. That timeout
    /// covers the whole handler chain, so pacing inside it spends the caller's budget on a wait this
    /// library imposed: with the default thirty-second timeout and thirty-second
    /// <see cref="IbkrRateLimitingOptions.MaxWait"/>, the longest permitted wait is the entire
    /// budget, and the request fails as a timeout without a byte having been sent.
    /// <see cref="IbkrApiClient"/> calls this before handing the request to
    /// <see cref="HttpClient"/> for that reason.
    /// </remarks>
    /// <param name="method">The request method.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    internal async Task AcquireAsync(HttpMethod method, Uri? requestUri, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return;
        }

        var endpointLimiter = Find(method, requestUri);

        // A rejection IBKR has already sent outranks whatever this library believes the limit to be,
        // so it is waited out first -- and before any permit is taken, so that a permit is not spent
        // sitting in a hold and then counted against a window it never used.
        await _backoff
            .WaitAsync(BackoffKey(method, requestUri, endpointLimiter), MaxWait, cancellationToken)
            .ConfigureAwait(false);

        // The endpoint permit is taken next so that a request waiting on a tight per-endpoint
        // limit does not also hold the global permit while it waits.
        if (endpointLimiter is not null)
        {
            await endpointLimiter.AcquireAsync(MaxWait, cancellationToken).ConfigureAwait(false);
        }

        if (Global is { } global)
        {
            await global.AcquireAsync(MaxWait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records that IBKR answered a request with <c>429 Too Many Requests</c>, holding further
    /// requests to the same limit until the window it asked for has elapsed.
    /// </summary>
    /// <param name="method">The rejected request's method.</param>
    /// <param name="requestUri">The rejected request's URI.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> header, when IBKR sent one.</param>
    /// <returns>
    /// How long requests are now held for, or <see langword="null"/> when nothing was held because
    /// pacing is disabled.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The hold is attributed to the limit that paced the request, or to the exact path when no
    /// limit matched. It is not applied globally: a <c>429</c> from one endpoint is usually that
    /// endpoint's own limit, and stalling every other call in the process on it would turn one
    /// rejected request into an outage. The cost of that choice is that an address IBKR has put in
    /// its ten-minute penalty box shows up as a rejection per path rather than a single stall.
    /// </para>
    /// <para>
    /// IBKR does not document sending <c>Retry-After</c> at all, so the window usually has to be
    /// assumed: the matched limit's own window when there is one, on the grounds that it is the
    /// figure this library already believed, and <see cref="DefaultRetryAfter"/> otherwise.
    /// </para>
    /// </remarks>
    internal Duration? Penalize(HttpMethod method, Uri? requestUri, RetryConditionHeaderValue? retryAfter)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (!Enabled)
        {
            // Turning pacing off turns the feedback off with it. The caller is still told what IBKR
            // asked for -- IbkrApiClient reads the header for the exception -- but nothing is held
            // on their behalf, and saying otherwise would be a lie about what happens next.
            return null;
        }

        var endpointLimiter = Find(method, requestUri);
        var backoff = ResolveRetryAfter(retryAfter) ?? endpointLimiter?.Window ?? DefaultRetryAfter;
        return _backoff.Record(BackoffKey(method, requestUri, endpointLimiter), backoff);
    }

    /// <summary>Returns the per-endpoint limiter matching a request, when one applies.</summary>
    internal SlidingWindowLimiter? Find(HttpMethod method, Uri? requestUri)
    {
        if (requestUri is null)
        {
            return null;
        }

        var path = PathOf(requestUri);

        foreach (var (template, limitMethod, limiter) in _endpointLimiters)
        {
            if ((limitMethod is null || string.Equals(limitMethod, method.Method, StringComparison.OrdinalIgnoreCase)) &&
                template.Matches(path))
            {
                return limiter;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Global?.Dispose();
        foreach (var (_, _, limiter) in _endpointLimiters)
        {
            limiter.Dispose();
        }
    }

    /// <summary>
    /// Names what a rejection is held against: the limiter that paced the request, so that
    /// everything sharing the limit is held with it, or the exact path when nothing paced it.
    /// </summary>
    private static string BackoffKey(HttpMethod method, Uri? requestUri, SlidingWindowLimiter? limiter) =>
        limiter?.Name
            ?? (requestUri is null ? method.Method : $"{method.Method} {PathOf(requestUri)}");

    private static string PathOf(Uri requestUri) =>
        requestUri.IsAbsoluteUri
            ? requestUri.AbsolutePath
            : requestUri.OriginalString.Split('?', 2)[0];

    /// <summary>
    /// Reads a <c>Retry-After</c> as a duration. The header may carry either a delay or an absolute
    /// date; a date already in the past reads as zero rather than as a negative hold.
    /// </summary>
    private Duration? ResolveRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            var stated = Duration.FromTimeSpan(delta);
            return stated > Duration.Zero ? stated : Duration.Zero;
        }

        if (retryAfter.Date is { } date)
        {
            var remaining = Instant.FromDateTimeOffset(date) - _scheduler.Now;
            return remaining > Duration.Zero ? remaining : Duration.Zero;
        }

        return null;
    }
}
