using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Authentication.OAuth2;

/// <summary>The response from <c>POST /oauth2/api/v1/token</c>.</summary>
public sealed record OAuth2AccessTokenResponse
{
    /// <summary>The serialized access token.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    /// <summary>The token type, for example <c>Bearer</c>.</summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>The space-delimited list of granted scopes.</summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>The number of seconds from issuance until the access token expires.</summary>
    [JsonPropertyName("expires_in")]
    public long? ExpiresInSeconds { get; init; }
}

/// <summary>The response from <c>POST /gw/api/v1/sso-sessions</c>.</summary>
/// <remarks>
/// The <c>access_token</c> here is the SSO gateway session credential, not the OAuth 2.0 access
/// token from the previous step. It is the value presented as <c>Bearer</c> on subsequent
/// <c>/v1/api</c> requests.
/// </remarks>
public sealed record OAuth2SsoSessionResponse
{
    /// <summary>The SSO session token.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    /// <summary>Whether the session is active.</summary>
    [JsonPropertyName("active")]
    public bool? Active { get; init; }

    /// <summary>The token type.</summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }
}
