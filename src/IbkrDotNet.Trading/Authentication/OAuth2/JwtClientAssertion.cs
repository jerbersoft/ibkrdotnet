using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IbkrDotNet.Trading.Authentication.OAuth2;

/// <summary>
/// Builds the RS256-signed JWT client assertions IBKR's OAuth 2.0 flow uses in place of a static
/// client secret (RFC 7523).
/// </summary>
/// <remarks>
/// The two steps of the flow share this construction but differ in their claim sets and in how the
/// assertion is transmitted, so both are built here to keep the encoding identical between them.
/// Any difference in JSON serialization or base64url encoding between the steps produces a
/// structurally valid but unverifiable signature.
/// </remarks>
internal static class JwtClientAssertion
{
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        // Compact, no whitespace: the signed bytes must be reproducible exactly.
        WriteIndented = false,
    };

    /// <summary>
    /// Builds the assertion for <c>POST /oauth2/api/v1/token</c>.
    /// </summary>
    /// <remarks>
    /// <c>aud</c> is the literal path <c>/token</c>, not the fully qualified endpoint URL. The
    /// assertion is deliberately short-lived because it authenticates one token request rather than
    /// a session, and <c>iat</c> is backdated to tolerate clock skew against IBKR's servers.
    /// </remarks>
    public static string ForAccessToken(
        OAuth2Options options,
        long issuedAtUnixSeconds,
        RSA privateKey)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["iss"] = options.ClientId,
            ["sub"] = options.ClientId,
            ["aud"] = "/token",
            ["exp"] = issuedAtUnixSeconds + 20,
            ["iat"] = issuedAtUnixSeconds - 10,
        };

        return Build(options.ClientKeyId, claims, privateKey);
    }

    /// <summary>
    /// Builds the assertion for <c>POST /gw/api/v1/sso-sessions</c>.
    /// </summary>
    /// <remarks>
    /// This claim set carries <c>ip</c> and <c>credential</c> and has no <c>sub</c> or <c>aud</c>,
    /// unlike the access-token assertion. It is issued with a 24-hour expiry because it authorizes a
    /// session rather than a single request.
    /// </remarks>
    public static string ForSsoSession(
        OAuth2Options options,
        string clientIpAddress,
        long issuedAtUnixSeconds,
        RSA privateKey)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ip"] = clientIpAddress,
            ["credential"] = options.Credential,
            ["iss"] = options.ClientId,
            ["exp"] = issuedAtUnixSeconds + 86400,
            ["iat"] = issuedAtUnixSeconds,
        };

        return Build(options.ClientKeyId, claims, privateKey);
    }

    private static string Build(string keyId, Dictionary<string, object> claims, RSA privateKey)
    {
        var header = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = keyId,
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, CompactJson));
        var encodedClaims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims, CompactJson));
        var payload = $"{encodedHeader}.{encodedClaims}";

        var signature = privateKey.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{payload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) => Base64Url.EncodeToString(value);
}
