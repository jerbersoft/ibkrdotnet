namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Paces outgoing requests to stay inside Interactive Brokers' published rate limits.
/// </summary>
/// <remarks>
/// The handler only delays; it does not interpret responses. A <c>429</c> that arrives anyway is
/// surfaced by <see cref="IbkrApiClient"/>, which has the request context needed to describe it.
/// The limiter state lives in <see cref="IbkrRateLimiterRegistry"/> so it survives the handler
/// rotation <c>IHttpClientFactory</c> performs.
/// </remarks>
public sealed class IbkrRateLimitHandler : DelegatingHandler
{
    private readonly IbkrRateLimiterRegistry _registry;

    /// <summary>Creates the handler.</summary>
    /// <param name="registry">The shared limiters.</param>
    public IbkrRateLimitHandler(IbkrRateLimiterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_registry.Enabled)
        {
            // The endpoint permit is taken first so that a request waiting on a tight per-endpoint
            // limit does not also hold the global permit while it waits.
            var endpointLimiter = _registry.Find(request.Method, request.RequestUri);
            if (endpointLimiter is not null)
            {
                await endpointLimiter.AcquireAsync(_registry.MaxWait, cancellationToken).ConfigureAwait(false);
            }

            if (_registry.Global is { } global)
            {
                await global.AcquireAsync(_registry.MaxWait, cancellationToken).ConfigureAwait(false);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
