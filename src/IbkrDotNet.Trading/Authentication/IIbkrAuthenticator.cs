namespace IbkrDotNet.Trading.Authentication;

/// <summary>
/// Applies Interactive Brokers credentials to an outgoing request.
/// </summary>
/// <remarks>
/// IBKR offers three ways in, and they differ only in how a request is credentialed — the resource
/// paths and payloads are identical across all of them. That is why authentication is a single
/// abstraction rather than three clients:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="ClientPortalGatewayAuthenticator"/> — the retail path. A local Java gateway performs
/// the credential exchange in a browser and proxies requests, so nothing needs signing here.
/// </description>
/// </item>
/// <item>
/// <description>
/// OAuth 2.0 — for Organizations, Financial Advisors and IBrokers. A signed JWT assertion buys an
/// access token, which buys an SSO session token presented as a bearer credential.
/// </description>
/// </item>
/// <item>
/// <description>
/// OAuth 1.0a — for Financial Advisors, organizations and third-party vendors. A Diffie-Hellman
/// handshake derives a live session token that signs each request.
/// </description>
/// </item>
/// </list>
/// </remarks>
public interface IIbkrAuthenticator
{
    /// <summary>A short name for the mechanism, used in diagnostics.</summary>
    string Scheme { get; }

    /// <summary>
    /// Whether requests must carry the <c>api={sessionToken}</c> cookie obtained from <c>/tickle</c>.
    /// </summary>
    /// <remarks>
    /// IBKR requires client-side cookie management for both OAuth flows. The Client Portal Gateway
    /// manages its own cookies, so it does not.
    /// </remarks>
    bool RequiresSessionCookie { get; }

    /// <summary>
    /// Applies credentials to the request, acquiring or refreshing them first if needed.
    /// </summary>
    /// <param name="request">The request about to be sent.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Http.IbkrAuthenticationException">Credentials could not be obtained.</exception>
    ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
