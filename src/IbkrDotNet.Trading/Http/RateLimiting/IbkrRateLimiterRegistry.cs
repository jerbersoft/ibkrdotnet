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

        // The endpoint permit is taken first so that a request waiting on a tight per-endpoint
        // limit does not also hold the global permit while it waits.
        if (Find(method, requestUri) is { } endpointLimiter)
        {
            await endpointLimiter.AcquireAsync(MaxWait, cancellationToken).ConfigureAwait(false);
        }

        if (Global is { } global)
        {
            await global.AcquireAsync(MaxWait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Returns the per-endpoint limiter matching a request, when one applies.</summary>
    internal SlidingWindowLimiter? Find(HttpMethod method, Uri? requestUri)
    {
        if (requestUri is null)
        {
            return null;
        }

        var path = requestUri.IsAbsoluteUri
            ? requestUri.AbsolutePath
            : requestUri.OriginalString.Split('?', 2)[0];

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
}
