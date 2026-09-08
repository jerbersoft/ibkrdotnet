namespace IbkrDotNet.Trading.Authentication;

/// <summary>
/// Authenticates through a locally running Client Portal Gateway.
/// </summary>
/// <remarks>
/// The gateway holds the credentials: the user logs in at <c>https://localhost:5000</c> in a
/// browser, and the gateway attaches session cookies to requests it proxies. There is nothing for
/// this authenticator to sign or attach, so it is deliberately a no-op — it exists so that the
/// gateway is selected the same way the OAuth mechanisms are, and so switching between them is a
/// configuration change rather than a code change.
/// </remarks>
public sealed class ClientPortalGatewayAuthenticator : IIbkrAuthenticator
{
    /// <summary>A shared instance; the type holds no state.</summary>
    public static ClientPortalGatewayAuthenticator Instance { get; } = new();

    /// <inheritdoc />
    public string Scheme => "ClientPortalGateway";

    /// <inheritdoc />
    public bool RequiresSessionCookie => false;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.CompletedTask;
    }
}
