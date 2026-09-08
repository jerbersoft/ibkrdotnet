using System.Globalization;
using System.Net.Http.Json;
using System.Numerics;
using System.Security.Cryptography;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Trading.Authentication.OAuth1a;

/// <summary>
/// Authenticates directly against <c>https://api.ibkr.com</c> using IBKR's OAuth 1.0a flow.
/// </summary>
/// <remarks>
/// <para>
/// A one-time handshake derives a live session token, after which every request is signed with it:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// The access token secret is decrypted with the RSA encryption key, producing the <c>prepend</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// A Diffie-Hellman challenge is posted to <c>/v1/api/oauth/live_session_token</c>, signed
/// RSA-SHA256 over a base string that begins with the <c>prepend</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// The shared secret is combined with the <c>prepend</c> under HMAC-SHA1 to derive the token, which
/// is then verified against the signature IBKR returned. A mismatch aborts.
/// </description>
/// </item>
/// <item>
/// <description>
/// Every subsequent request is signed HMAC-SHA256 with the token. This is the dividing line: the
/// handshake signs asymmetrically with the application's RSA key, ongoing traffic signs
/// symmetrically with the secret both sides now share.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class OAuth1aAuthenticator : IIbkrAuthenticator, IDisposable
{
    /// <summary>The name of the unauthenticated HTTP client used for the handshake.</summary>
    /// <remarks>The handshake must not run through the authenticating handler, which would recurse.</remarks>
    public const string HttpClientName = "IbkrDotNet.Trading.OAuth1a";

    private const string LiveSessionTokenPath = "/v1/api/oauth/live_session_token";

    private readonly HttpClient _httpClient;
    private readonly OAuth1aOptions _options;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _handshakeGate = new(1, 1);
    private readonly BigInteger _prime;

    private LiveSessionToken? _liveSessionToken;
    private bool _disposed;

    /// <summary>Creates the authenticator.</summary>
    /// <param name="httpClient">An HTTP client that does not run through the authenticating handler.</param>
    /// <param name="options">The OAuth 1.0a credentials.</param>
    /// <param name="clock">The clock used for timestamps and token expiry.</param>
    public OAuth1aAuthenticator(HttpClient httpClient, IOptions<OAuth1aOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _httpClient = httpClient;
        _options = options.Value;
        _clock = clock;
        _options.Validate();
        _prime = _options.ParsePrime();
    }

    /// <inheritdoc />
    public string Scheme => "OAuth1a";

    /// <inheritdoc />
    public bool RequiresSessionCookie => true;

    /// <inheritdoc />
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await GetLiveSessionTokenAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = _options.ConsumerKey,
            ["oauth_nonce"] = CreateNonce(),
            ["oauth_signature_method"] = "HMAC-SHA256",
            ["oauth_timestamp"] = CreateTimestamp(),
            ["oauth_token"] = _options.AccessToken,
        };

        // IBKR excludes the query string from the base string, signing only the path.
        var url = RequestUrlWithoutQuery(request);
        var baseString = OAuth1aSignatures.BuildBaseString(null, request.Method.Method, url, parameters);

        // The signature is percent-encoded before it goes into the header, and 'realm' is added
        // only after signing so it never contributes to the base string.
        parameters["oauth_signature"] =
            OAuth1aSignatures.PercentEncode(OAuth1aSignatures.SignHmacSha256(baseString, token.Token));
        parameters["realm"] = _options.Realm;

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            OAuth1aSignatures.BuildAuthorizationHeader(parameters));

        if (request.Headers.Accept.Count == 0)
        {
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
        }
    }

    /// <summary>Discards the live session token so the next request repeats the handshake.</summary>
    public void Invalidate()
    {
        _handshakeGate.Wait();
        try
        {
            _liveSessionToken = null;
        }
        finally
        {
            _handshakeGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _handshakeGate.Dispose();
        }
    }

    private async ValueTask<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
    {
        if (_liveSessionToken is { } cached && _clock.GetCurrentInstant() < cached.ExpiresAt)
        {
            return cached;
        }

        await _handshakeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_liveSessionToken is { } refreshed && _clock.GetCurrentInstant() < refreshed.ExpiresAt)
            {
                return refreshed;
            }

            _liveSessionToken = await PerformHandshakeAsync(cancellationToken).ConfigureAwait(false);
            return _liveSessionToken;
        }
        finally
        {
            _handshakeGate.Release();
        }
    }

    private async Task<LiveSessionToken> PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        byte[] prependBytes;
        using (var encryptionKey = _options.CreateEncryptionKey!())
        {
            prependBytes = OAuth1aSignatures.DecryptAccessTokenSecret(_options.AccessTokenSecret, encryptionKey);
        }

        var prepend = Convert.ToHexStringLower(prependBytes);
        var privateExponent = OAuth1aSignatures.GeneratePrivateExponent();
        var challenge = OAuth1aSignatures.ComputeChallenge(
            _options.DiffieHellmanGenerator,
            privateExponent,
            _prime);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = _options.ConsumerKey,
            ["oauth_nonce"] = CreateNonce(),
            ["oauth_timestamp"] = CreateTimestamp(),
            ["oauth_token"] = _options.AccessToken,
            ["oauth_signature_method"] = "RSA-SHA256",
            ["diffie_hellman_challenge"] = challenge,
        };

        var url = new Uri(_httpClient.BaseAddress!, LiveSessionTokenPath).GetLeftPart(UriPartial.Path);
        var baseString = OAuth1aSignatures.BuildBaseString(prepend, "POST", url, parameters);

        string signature;
        using (var signatureKey = _options.CreateSignatureKey!())
        {
            signature = OAuth1aSignatures.SignRsaSha256(baseString, signatureKey);
        }

        parameters["oauth_signature"] = OAuth1aSignatures.PercentEncode(signature);

        using var request = new HttpRequestMessage(HttpMethod.Post, LiveSessionTokenPath);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            OAuth1aSignatures.BuildLiveSessionTokenAuthorizationHeader(_options.Realm, parameters));

        var payload = await SendHandshakeAsync(request, cancellationToken).ConfigureAwait(false);

        if (payload.ServerPublicValue is not { Length: > 0 } serverPublic)
        {
            throw new IbkrAuthenticationException(
                $"IBKR's response to {LiveSessionTokenPath} carried no Diffie-Hellman value, so no " +
                "live session token can be derived.");
        }

        var computed = OAuth1aSignatures.DeriveLiveSessionToken(
            serverPublic,
            privateExponent,
            _prime,
            prependBytes);

        if (payload.LiveSessionTokenSignature is not { Length: > 0 } expectedSignature ||
            !OAuth1aSignatures.VerifyLiveSessionToken(computed, _options.ConsumerKey, expectedSignature))
        {
            // The comparison is the only gate on trusting the derived token. A mismatch means the
            // shared secret, the decrypted secret, or a byte-encoding detail disagrees with IBKR,
            // and the token must not be used.
            throw new IbkrAuthenticationException(
                "The live session token derived locally does not match the signature IBKR returned. " +
                "Check that the Diffie-Hellman prime matches the one issued with the consumer key, " +
                "and that the encryption and signing keys have not been swapped.");
        }

        return new LiveSessionToken(computed, payload.ResolveExpiry(_clock.GetCurrentInstant()));
    }

    private async Task<LiveSessionTokenResponse> SendHandshakeAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new IbkrAuthenticationException(
                $"The OAuth 1.0a handshake request to {LiveSessionTokenPath} could not be sent: {ex.Message}",
                ex)
            {
                Method = "POST",
                Path = LiveSessionTokenPath,
            };
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new IbkrAuthenticationException(
                    $"The OAuth 1.0a handshake failed with {(int)response.StatusCode} " +
                    $"{response.StatusCode}: {body}")
                {
                    StatusCode = response.StatusCode,
                    Method = "POST",
                    Path = LiveSessionTokenPath,
                    ResponseBody = body,
                };
            }

            return await response.Content
                       .ReadFromJsonAsync<LiveSessionTokenResponse>(IbkrJson.Options, cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new IbkrAuthenticationException(
                       $"The OAuth 1.0a handshake at {LiveSessionTokenPath} returned an empty body.");
        }
    }

    private static string RequestUrlWithoutQuery(HttpRequestMessage request)
    {
        var uri = request.RequestUri
            ?? throw new IbkrAuthenticationException(
                "An OAuth 1.0a request cannot be signed without a request URI.");

        return uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Path) : uri.OriginalString.Split('?', 2)[0];
    }

    private static string CreateNonce() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)).TrimStart('0') is { Length: > 0 } nonce
            ? nonce
            : "0";

    private string CreateTimestamp() =>
        _clock.GetCurrentInstant().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
