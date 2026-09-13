namespace IbkrDotNet.Trading.Authentication;

/// <summary>
/// A credential the WebSocket upgrade request carries as a query parameter.
/// </summary>
/// <param name="ParameterName">The query parameter name, <c>bearer_token</c> or <c>oauth_token</c>.</param>
/// <param name="Value">The credential value.</param>
/// <remarks>
/// A socket cannot be signed per request the way HTTP can, so IBKR moves the credential into the
/// upgrade request's query string: the SSO session token for OAuth 2.0, the access token for OAuth
/// 1.0a. The Client Portal Gateway needs neither, because the <c>api=</c> session cookie every
/// mechanism sends is enough for it.
/// </remarks>
public sealed record StreamingCredential(string ParameterName, string Value);
