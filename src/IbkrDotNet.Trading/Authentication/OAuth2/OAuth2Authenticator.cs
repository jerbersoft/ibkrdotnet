using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Trading.Authentication.OAuth2;

/// <summary>
/// Authenticates directly against <c>https://api.ibkr.com</c> using IBKR's OAuth 2.0 flow.
/// </summary>
/// <remarks>
/// <para>Two exchanges precede the first API call:</para>
/// <list type="number">
/// <item>
/// <description>
/// A short-lived RS256 assertion is posted to <c>/oauth2/api/v1/token</c> as a form field, yielding
/// an access token.
/// </description>
/// </item>
/// <item>
/// <description>
/// A second, differently shaped assertion is posted to <c>/gw/api/v1/sso-sessions</c> as the raw
/// request body with <c>Content-Type: application/jwt</c>, authorized by the access token from step
/// one. Its response is the SSO session token used as the <c>Bearer</c> credential thereafter.
/// </description>
/// </item>
/// </list>
/// <para>
/// The two <c>access_token</c> values are unrelated despite sharing a field name; only the second is
/// ever presented to a <c>/v1/api</c> endpoint.
/// </para>
/// </remarks>
public sealed class OAuth2Authenticator : IIbkrAuthenticator, IDisposable
{
    /// <summary>The name of the unauthenticated HTTP client used for the token exchanges.</summary>
    /// <remarks>
    /// These exchanges must not run through the authenticating handler, which would recurse.
    /// </remarks>
    public const string HttpClientName = "IbkrDotNet.Trading.OAuth2";

    private const string TokenPath = "/oauth2/api/v1/token";
    private const string SsoSessionPath = "/gw/api/v1/sso-sessions";

    private readonly HttpClient _httpClient;
    private readonly OAuth2Options _options;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private string? _sessionToken;
    private Instant _sessionTokenExpiresAt = Instant.MinValue;
    private bool _disposed;

    /// <summary>Creates the authenticator.</summary>
    /// <param name="httpClient">An HTTP client that does not run through the authenticating handler.</param>
    /// <param name="options">The OAuth 2.0 credentials.</param>
    /// <param name="clock">The clock used for token lifetimes and JWT timestamps.</param>
    public OAuth2Authenticator(HttpClient httpClient, IOptions<OAuth2Options> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _httpClient = httpClient;
        _options = options.Value;
        _clock = clock;
        _options.Validate();
    }

    /// <inheritdoc />
    public string Scheme => "OAuth2";

    /// <inheritdoc />
    public bool RequiresSessionCookie => true;

    /// <inheritdoc />
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await GetSessionTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Discards the cached SSO session token so the next request establishes a new session.
    /// </summary>
    /// <remarks>
    /// Called when IBKR reports the brokerage session is no longer authenticated, which the token's
    /// own lifetime does not reflect.
    /// </remarks>
    public void Invalidate()
    {
        _refreshGate.Wait();
        try
        {
            _sessionToken = null;
            _sessionTokenExpiresAt = Instant.MinValue;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _refreshGate.Dispose();
        }
    }

    private async ValueTask<string> GetSessionTokenAsync(CancellationToken cancellationToken)
    {
        if (_sessionToken is { Length: > 0 } cached && _clock.GetCurrentInstant() < _sessionTokenExpiresAt)
        {
            return cached;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while this one waited.
            if (_sessionToken is { Length: > 0 } refreshed && _clock.GetCurrentInstant() < _sessionTokenExpiresAt)
            {
                return refreshed;
            }

            var accessToken = await RequestAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var sessionToken = await RequestSsoSessionAsync(accessToken, cancellationToken).ConfigureAwait(false);

            _sessionToken = sessionToken;
            _sessionTokenExpiresAt = _clock.GetCurrentInstant() + _options.SessionTokenLifetime;
            return sessionToken;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<string> RequestAccessTokenAsync(CancellationToken cancellationToken)
    {
        var issuedAt = _clock.GetCurrentInstant().ToUnixTimeSeconds();

        string assertion;
        using (var key = _options.CreatePrivateKey!())
        {
            assertion = JwtClientAssertion.ForAccessToken(_options, issuedAt, key);
        }

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(
                "client_assertion_type",
                "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
            new KeyValuePair<string, string>("client_assertion", assertion),
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", _options.Scope),
        ]);

        using var response = await PostAsync(TokenPath, content, cancellationToken).ConfigureAwait(false);
        var token = await ReadAsync<OAuth2AccessTokenResponse>(response, TokenPath, cancellationToken)
            .ConfigureAwait(false);

        return token.AccessToken is { Length: > 0 } accessToken
            ? accessToken
            : throw new IbkrAuthenticationException(
                $"IBKR accepted the client assertion at {TokenPath} but returned no access_token.");
    }

    private async Task<string> RequestSsoSessionAsync(string accessToken, CancellationToken cancellationToken)
    {
        var ipAddress = await ResolveClientIpAddressAsync(cancellationToken).ConfigureAwait(false);
        var issuedAt = _clock.GetCurrentInstant().ToUnixTimeSeconds();

        string assertion;
        using (var key = _options.CreatePrivateKey!())
        {
            assertion = JwtClientAssertion.ForSsoSession(_options, ipAddress, issuedAt, key);
        }

        // This endpoint takes the bare compact JWS as its body, not a form field.
        using var content = new StringContent(assertion, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jwt");

        using var request = new HttpRequestMessage(HttpMethod.Post, SsoSessionPath) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await SendAsync(request, SsoSessionPath, cancellationToken).ConfigureAwait(false);
        var session = await ReadAsync<OAuth2SsoSessionResponse>(response, SsoSessionPath, cancellationToken)
            .ConfigureAwait(false);

        return session.AccessToken is { Length: > 0 } sessionToken
            ? sessionToken
            : throw new IbkrAuthenticationException(
                $"IBKR accepted the assertion at {SsoSessionPath} but returned no session token.");
    }

    private async ValueTask<string> ResolveClientIpAddressAsync(CancellationToken cancellationToken)
    {
        if (_options.ClientIpAddress is { Length: > 0 } configured)
        {
            return configured;
        }

        var resolved = await _options.ClientIpAddressResolver!(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(resolved)
            ? throw new IbkrAuthenticationException(
                "The configured client IP address resolver returned no address. IBKR validates this " +
                "claim against the address the request arrives from, so it cannot be omitted.")
            : resolved;
    }

    private Task<HttpResponseMessage> PostAsync(string path, HttpContent content, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        return SendAsync(request, path, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new IbkrAuthenticationException($"The OAuth 2.0 request to {path} could not be sent: {ex.Message}", ex)
            {
                Method = "POST",
                Path = path,
            };
        }
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new IbkrAuthenticationException(
                $"The OAuth 2.0 request to {path} failed with {(int)response.StatusCode} " +
                $"{response.StatusCode}: {body}")
            {
                StatusCode = response.StatusCode,
                Method = "POST",
                Path = path,
                ResponseBody = body,
            };
        }

        var value = await response.Content
            .ReadFromJsonAsync<T>(IbkrJson.Options, cancellationToken)
            .ConfigureAwait(false);

        return value ?? throw new IbkrAuthenticationException(
            $"The OAuth 2.0 request to {path} returned an empty body.");
    }
}
