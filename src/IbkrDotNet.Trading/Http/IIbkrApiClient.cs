namespace IbkrDotNet.Trading.Http;

/// <summary>
/// Sends requests to the Interactive Brokers Web API and deserializes their responses.
/// </summary>
/// <remarks>
/// The endpoint clients are built on this. It is public so that callers can reach endpoints this
/// library has not modelled yet without dropping to a raw <see cref="HttpClient"/> and losing
/// authentication, rate limiting and error mapping.
/// </remarks>
public interface IIbkrApiClient
{
    /// <summary>Sends a request and deserializes the JSON response.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IbkrApiException">IBKR returned an error response.</exception>
    Task<TResponse> SendAsync<TResponse>(IbkrRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends a request and discards the response body.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IbkrApiException">IBKR returned an error response.</exception>
    Task SendAsync(IbkrRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request and returns the raw response, for endpoints whose body is not JSON or whose
    /// shape varies in ways a single type cannot express.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// The response is returned regardless of status code and is not checked for success; the caller
    /// owns it and must dispose it.
    /// </remarks>
    Task<HttpResponseMessage> SendRawAsync(IbkrRequest request, CancellationToken cancellationToken = default);
}
