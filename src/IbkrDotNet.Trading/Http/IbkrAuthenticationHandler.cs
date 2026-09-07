using System.Net.Http.Headers;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.Options;

namespace IbkrDotNet.Trading.Http;

/// <summary>
/// Credentials each outgoing request using the configured <see cref="IIbkrAuthenticator"/>, and
/// attaches the brokerage session cookie when the mechanism requires it.
/// </summary>
public sealed class IbkrAuthenticationHandler : DelegatingHandler
{
    private const string SessionCookiePrefix = "api=";

    private readonly IIbkrAuthenticator _authenticator;
    private readonly IbkrSessionState _sessionState;
    private readonly string _userAgent;

    /// <summary>Creates the handler.</summary>
    /// <param name="authenticator">The authentication mechanism in use.</param>
    /// <param name="sessionState">The shared brokerage session token.</param>
    /// <param name="options">The client options.</param>
    public IbkrAuthenticationHandler(
        IIbkrAuthenticator authenticator,
        IbkrSessionState sessionState,
        IOptions<IbkrTradingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(sessionState);
        ArgumentNullException.ThrowIfNull(options);

        _authenticator = authenticator;
        _sessionState = sessionState;
        _userAgent = options.Value.UserAgent;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // IBKR asks that every client identify itself, and the Client Portal Gateway in particular
        // rejects requests without one.
        if (request.Headers.UserAgent.Count == 0 &&
            ProductInfoHeaderValue.TryParse(_userAgent, out var product))
        {
            request.Headers.UserAgent.Add(product);
        }

        await _authenticator.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false);

        if (_authenticator.RequiresSessionCookie &&
            _sessionState.SessionToken is { Length: > 0 } token &&
            !HasSessionCookie(request))
        {
            request.Headers.Add("Cookie", SessionCookiePrefix + token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasSessionCookie(HttpRequestMessage request) =>
        request.Headers.TryGetValues("Cookie", out var cookies) &&
        cookies.Any(c => c.Contains(SessionCookiePrefix, StringComparison.Ordinal));
}
