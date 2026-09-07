using System.Net;

namespace IbkrDotNet.Trading.Http;

/// <summary>
/// The base type for every failure reported by the Interactive Brokers Web API.
/// </summary>
public class IbkrApiException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    public IbkrApiException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public IbkrApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The HTTP status code returned by IBKR, when the request reached the server.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>The HTTP method of the failed request.</summary>
    public string? Method { get; init; }

    /// <summary>The path of the failed request, excluding the query string.</summary>
    public string? Path { get; init; }

    /// <summary>
    /// The raw response body, truncated for logging. IBKR returns error detail in several shapes
    /// (<c>error</c>, <c>message</c>, <c>statusCode</c>), so the body is preserved verbatim.
    /// </summary>
    public string? ResponseBody { get; init; }
}

/// <summary>
/// Thrown when a request could not be authenticated, or when the brokerage session required by the
/// endpoint is not established.
/// </summary>
/// <remarks>
/// IBKR sessions are two-tiered: an outer read-only session gates every request, and a separate
/// brokerage session gates everything behind <c>/iserver</c>. A 401 on an <c>/iserver</c> endpoint
/// most often means the brokerage session lapsed rather than that the credentials are wrong.
/// </remarks>
public sealed class IbkrAuthenticationException : IbkrApiException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    public IbkrAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public IbkrAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a request would exceed a documented Interactive Brokers rate limit, or when IBKR
/// answered with <c>429 Too Many Requests</c>.
/// </summary>
/// <remarks>
/// IBKR may place a violating IP address in a ten-minute penalty box, and repeat violators can be
/// blocked until the issue is resolved, so the client refuses to send a request it knows would
/// breach a limit rather than letting it through and hoping.
/// </remarks>
public sealed class IbkrRateLimitExceededException : IbkrApiException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    public IbkrRateLimitExceededException(string message)
        : base(message)
    {
    }

    /// <summary>How long the caller should wait before retrying, when known.</summary>
    public NodaTime.Duration? RetryAfter { get; init; }

    /// <summary>The rate limit that was breached, when the client detected it locally.</summary>
    public string? Limit { get; init; }
}
