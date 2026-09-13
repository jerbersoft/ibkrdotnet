using System.Security.Cryptography;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Authentication.OAuth1a;
using IbkrDotNet.Trading.Authentication.OAuth2;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Streaming;

public class StreamingCredentialTests
{
    [Fact]
    public async Task The_gateway_needs_nothing_beyond_the_session_cookie()
    {
        // The gateway takes the interface's default answer, reachable only through the interface.
        var credential = await ((IIbkrAuthenticator)ClientPortalGatewayAuthenticator.Instance)
            .GetStreamingCredentialAsync(TestContext.Current.CancellationToken);

        Assert.Null(credential);
    }

    [Fact]
    public async Task OAuth2_presents_the_sso_session_token_as_bearer_token()
    {
        using var key = RSA.Create(2048);
        var stub = new StubHttpMessageHandler();
        stub.RespondWithJson("""{"access_token":"ACCESS","token_type":"Bearer","expires_in":86399}""");
        stub.RespondWithJson("""{"access_token":"SESSION","active":true,"token_type":"Bearer"}""");
        using var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };

        var options = new OAuth2Options
        {
            ClientId = "TESTCLIENT",
            ClientKeyId = "key-1",
            Credential = "testuser",
            ClientIpAddress = "203.0.113.7",
        };
        options.UsePrivateKeyPem(key.ExportRSAPrivateKeyPem());

        using var authenticator = new OAuth2Authenticator(
            httpClient,
            Options.Create(options),
            new FakeClock(Instant.FromUtc(2024, 5, 1, 12, 0)));

        var credential = await authenticator.GetStreamingCredentialAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new StreamingCredential("bearer_token", "SESSION"), credential);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task OAuth1a_presents_the_access_token_as_oauth_token_without_a_handshake()
    {
        using var key = RSA.Create(2048);
        var pem = key.ExportRSAPrivateKeyPem();
        var stub = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };

        var options = new OAuth1aOptions
        {
            ConsumerKey = "TESTCONS",
            Realm = OAuth1aOptions.TestRealm,
            AccessToken = "a1b2c3d4",
            AccessTokenSecret = "c2VjcmV0",
            DiffieHellmanPrime = "f7e75fdc469067ffdc4e847c51f452df",
        };
        options.UseEncryptionKeyPem(pem);
        options.UseSignatureKeyPem(pem);

        using var authenticator = new OAuth1aAuthenticator(
            httpClient,
            Options.Create(options),
            new FakeClock(Instant.FromUtc(2024, 5, 1, 12, 0)));

        var credential = await authenticator.GetStreamingCredentialAsync(TestContext.Current.CancellationToken);

        // The socket is identified by the permanent token; the live session token has already
        // signed the /tickle that produced the session cookie.
        Assert.Equal(new StreamingCredential("oauth_token", "a1b2c3d4"), credential);
        Assert.Empty(stub.Requests);
    }
}
