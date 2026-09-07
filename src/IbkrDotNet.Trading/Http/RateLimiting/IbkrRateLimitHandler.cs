using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Paces outgoing requests to stay inside Interactive Brokers' published rate limits.
/// </summary>
/// <remarks>
/// The handler only delays; it does not interpret responses. A <c>429</c> that arrives anyway is
/// surfaced by <see cref="IbkrApiClient"/>, which has the request context needed to describe it.
/// </remarks>
public sealed class IbkrRateLimitHandler : DelegatingHandler
{
    private readonly List<(PathTemplate Template, string? Method, SlidingWindowLimiter Limiter)> _endpointLimiters;
    private readonly SlidingWindowLimiter? _globalLimiter;
    private readonly Duration _maxWait;
    private readonly bool _enabled;
    private bool _disposed;

    /// <summary>Creates the handler from configured options.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="clock">The clock used to measure windows.</param>
    public IbkrRateLimitHandler(IOptions<IbkrTradingOptions> options, IClock clock)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)),
               new SystemDelayScheduler(clock))
    {
    }

    internal IbkrRateLimitHandler(IbkrTradingOptions options, IDelayScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scheduler);

        var limiting = options.RateLimiting;
        _enabled = limiting.Enabled;
        _maxWait = limiting.MaxWait;

        _globalLimiter = limiting.EnforceGlobalLimit
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

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_enabled)
        {
            // The endpoint permit is taken first so that a request waiting on a tight per-endpoint
            // limit does not also hold the global permit while it waits.
            var endpointLimiter = FindLimiter(request);
            if (endpointLimiter is not null)
            {
                await endpointLimiter.AcquireAsync(_maxWait, cancellationToken).ConfigureAwait(false);
            }

            if (_globalLimiter is not null)
            {
                await _globalLimiter.AcquireAsync(_maxWait, cancellationToken).ConfigureAwait(false);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _globalLimiter?.Dispose();
            foreach (var (_, _, limiter) in _endpointLimiters)
            {
                limiter.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private SlidingWindowLimiter? FindLimiter(HttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            return null;
        }

        var path = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri.AbsolutePath
            : request.RequestUri.OriginalString.Split('?', 2)[0];

        foreach (var (template, method, limiter) in _endpointLimiters)
        {
            if ((method is null || string.Equals(method, request.Method.Method, StringComparison.OrdinalIgnoreCase)) &&
                template.Matches(path))
            {
                return limiter;
            }
        }

        return null;
    }
}
