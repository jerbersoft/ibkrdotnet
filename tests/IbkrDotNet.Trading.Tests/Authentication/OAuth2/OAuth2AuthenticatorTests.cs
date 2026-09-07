using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IbkrDotNet.Trading.Authentication.OAuth2;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Authentication.OAuth2;

public class OAuth2AuthenticatorTests
{
    private static readonly Instant Start = Instant.FromUtc(2024, 5, 1, 12, 0, 0);

    private static RSA CreateKey() => RSA.Create(2048);

    private static OAuth2Options CreateOptions(RSA key) => new()
    {
        ClientId = "TESTCLIENT",
        ClientKeyId = "key-1",
        Credential = "testuser",
        Scope = "sso-sessions.write",
        ClientIpAddress = "203.0.113.7",
        // The key is owned by the test; hand out a non-owning view so disposal after signing is safe.
        CreatePrivateKey = () =>
        {
            var copy = RSA.Create();
            copy.ImportParameters(key.ExportParameters(includePrivateParameters: true));
            return copy;
        },
    };

    private static (OAuth2Authenticator Auth, StubHttpMessageHandler Stub, FakeClock Clock) Build(
        RSA key,
        Action<OAuth2Options>? configure = null)
    {
        var options = CreateOptions(key);
        configure?.Invoke(options);

        var stub = new StubHttpMessageHandler();
        stub.RespondWithJson("""{"access_token":"ACCESS","token_type":"Bearer","expires_in":86399}""");
        stub.RespondWithJson("""{"access_token":"SESSION","active":true,"token_type":"Bearer"}""");

        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };
        var clock = new FakeClock(Start);
        return (new OAuth2Authenticator(httpClient, Options.Create(options), clock), stub, clock);
    }

    private static JsonElement DecodeSegment(string jws, int index)
    {
        var segment = jws.Split('.')[index];
        return JsonDocument.Parse(Base64Url.DecodeFromChars(segment)).RootElement.Clone();
    }

    [Fact]
    public async Task Performs_both_exchanges_and_presents_the_session_token_as_the_bearer()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal("/oauth2/api/v1/token", stub.Requests[0].Path);
        Assert.Equal("/gw/api/v1/sso-sessions", stub.Requests[1].Path);

        // The bearer is the SSO session token, not the OAuth access token from the first step.
        Assert.Equal("SESSION", request.Headers.Authorization?.Parameter);
        auth.Dispose();
    }

    [Fact]
    public async Task Sends_the_access_token_assertion_as_a_form_field()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var body = stub.Requests[0].Body!;
        Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
        Assert.Contains(
            "client_assertion_type=urn%3Aietf%3Aparams%3Aoauth%3Aclient-assertion-type%3Ajwt-bearer",
            body,
            StringComparison.Ordinal);
        Assert.Contains("scope=sso-sessions.write", body, StringComparison.Ordinal);
        auth.Dispose();
    }

    [Fact]
    public async Task Builds_the_access_token_claims_exactly_as_ibkr_documents_them()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var form = System.Web.HttpUtility.ParseQueryString(stub.Requests[0].Body!);
        var assertion = form["client_assertion"]!;

        var header = DecodeSegment(assertion, 0);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
        Assert.Equal("key-1", header.GetProperty("kid").GetString());

        var claims = DecodeSegment(assertion, 1);
        Assert.Equal("TESTCLIENT", claims.GetProperty("iss").GetString());
        Assert.Equal("TESTCLIENT", claims.GetProperty("sub").GetString());

        // 'aud' is the literal path '/token', not the full endpoint URL.
        Assert.Equal("/token", claims.GetProperty("aud").GetString());

        var now = Start.ToUnixTimeSeconds();
        Assert.Equal(now + 20, claims.GetProperty("exp").GetInt64());

        // 'iat' is backdated to tolerate clock skew against IBKR's servers.
        Assert.Equal(now - 10, claims.GetProperty("iat").GetInt64());
        auth.Dispose();
    }

    [Fact]
    public async Task Builds_the_sso_session_claims_with_ip_and_credential_and_no_aud()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var assertion = stub.Requests[1].Body!;
        var claims = DecodeSegment(assertion, 1);

        Assert.Equal("203.0.113.7", claims.GetProperty("ip").GetString());
        Assert.Equal("testuser", claims.GetProperty("credential").GetString());
        Assert.Equal("TESTCLIENT", claims.GetProperty("iss").GetString());
        Assert.Equal(Start.ToUnixTimeSeconds() + 86400, claims.GetProperty("exp").GetInt64());

        // This step's assertion carries neither 'sub' nor 'aud'.
        Assert.False(claims.TryGetProperty("sub", out _));
        Assert.False(claims.TryGetProperty("aud", out _));
        auth.Dispose();
    }

    [Fact]
    public async Task Sends_the_sso_assertion_as_a_raw_jwt_body_with_the_access_token_as_bearer()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var sso = stub.Requests[1];
        Assert.Equal("Bearer ACCESS", sso.Headers["Authorization"]);
        Assert.Equal(3, sso.Body!.Split('.').Length);
        auth.Dispose();
    }

    [Fact]
    public async Task Produces_a_signature_ibkrs_registered_public_key_can_verify()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var assertion = stub.Requests[1].Body!;
        var parts = assertion.Split('.');
        var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64Url.DecodeFromChars(parts[2]);

        Assert.True(key.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        auth.Dispose();
    }

    [Fact]
    public async Task Reuses_the_session_token_until_its_lifetime_elapses()
    {
        using var key = CreateKey();
        var (auth, stub, clock) = Build(key, o => o.SessionTokenLifetime = Duration.FromHours(1));

        using (var first = new HttpRequestMessage(HttpMethod.Get, "/v1/api/a"))
        {
            await auth.AuthenticateAsync(first, TestContext.Current.CancellationToken);
        }

        clock.Advance(Duration.FromMinutes(59));
        using (var second = new HttpRequestMessage(HttpMethod.Get, "/v1/api/b"))
        {
            await auth.AuthenticateAsync(second, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, stub.Requests.Count);

        stub.RespondWithJson("""{"access_token":"ACCESS2"}""");
        stub.RespondWithJson("""{"access_token":"SESSION2"}""");
        clock.Advance(Duration.FromMinutes(2));

        using (var third = new HttpRequestMessage(HttpMethod.Get, "/v1/api/c"))
        {
            await auth.AuthenticateAsync(third, TestContext.Current.CancellationToken);
            Assert.Equal("SESSION2", third.Headers.Authorization?.Parameter);
        }

        Assert.Equal(4, stub.Requests.Count);
        auth.Dispose();
    }

    [Fact]
    public async Task Re_authenticates_after_being_invalidated()
    {
        using var key = CreateKey();
        var (auth, stub, _) = Build(key);

        using (var first = new HttpRequestMessage(HttpMethod.Get, "/v1/api/a"))
        {
            await auth.AuthenticateAsync(first, TestContext.Current.CancellationToken);
        }

        auth.Invalidate();
        stub.RespondWithJson("""{"access_token":"ACCESS2"}""");
        stub.RespondWithJson("""{"access_token":"SESSION2"}""");

        using (var second = new HttpRequestMessage(HttpMethod.Get, "/v1/api/b"))
        {
            await auth.AuthenticateAsync(second, TestContext.Current.CancellationToken);
            Assert.Equal("SESSION2", second.Headers.Authorization?.Parameter);
        }

        auth.Dispose();
    }

    [Fact]
    public async Task Reports_a_rejected_assertion_with_ibkrs_own_error_body()
    {
        using var key = CreateKey();
        var options = CreateOptions(key);
        var stub = new StubHttpMessageHandler();
        stub.RespondWith(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""");

        using var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };
        using var auth = new OAuth2Authenticator(httpClient, Options.Create(options), new FakeClock(Start));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/iserver/accounts");

        var ex = await Assert.ThrowsAsync<IbkrAuthenticationException>(
            () => auth.AuthenticateAsync(request, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("invalid_client", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_guess_the_client_ip_address()
    {
        using var key = CreateKey();
        var options = CreateOptions(key);
        options.ClientIpAddress = null;

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("ClientIpAddress", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Requires_a_private_key()
    {
        using var key = CreateKey();
        var options = CreateOptions(key);
        options.CreatePrivateKey = null;

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("CreatePrivateKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Loads_a_private_key_from_pem()
    {
        using var key = CreateKey();
        var options = new OAuth2Options().UsePrivateKeyPem(key.ExportRSAPrivateKeyPem());

        using var loaded = options.CreatePrivateKey!();

        Assert.Equal(key.ExportRSAPublicKeyPem(), loaded.ExportRSAPublicKeyPem());
    }
}
