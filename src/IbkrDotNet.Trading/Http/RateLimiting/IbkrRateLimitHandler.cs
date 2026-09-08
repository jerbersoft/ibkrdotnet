using IbkrDotNet.Trading.Configuration;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Paces outgoing requests to stay inside Interactive Brokers' published rate limits.
/// </summary>
/// <remarks>
/// <para>
/// The handler only delays; it does not interpret responses. A <c>429</c> that arrives anyway is
/// surfaced by <see cref="IbkrApiClient"/>, which has the request context needed to describe it.
/// The limiter state lives in <see cref="IbkrRateLimiterRegistry"/> so it survives the handler
/// rotation <c>IHttpClientFactory</c> performs.
/// </para>
/// <para>
/// <strong>This is not how the dependency injection package paces requests, and it is the second
/// choice.</strong> <see cref="HttpClient.Timeout"/> covers the whole handler chain, so a wait
/// taken here is spent out of the caller's request budget: a request delayed for the default
/// thirty-second <see cref="IbkrRateLimitingOptions.MaxWait"/> exhausts the default thirty-second
/// timeout and fails as though IBKR never answered, having never been sent. Use this handler only
/// on an <see cref="HttpClient"/> whose timeout is <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
/// or comfortably larger than the longest wait a limit can impose. <see cref="IbkrApiClient"/>
/// paces before it reaches <see cref="HttpClient"/> instead, where the wait costs nothing.
/// </para>
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

        await _registry.AcquireAsync(request.Method, request.RequestUri, cancellationToken)
            .ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
