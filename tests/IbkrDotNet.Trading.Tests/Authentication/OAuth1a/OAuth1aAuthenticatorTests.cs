using System.Globalization;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IbkrDotNet.Trading.Authentication.OAuth1a;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Authentication.OAuth1a;

/// <summary>
/// Exercises the full handshake against a stand-in for IBKR that performs its half of the
/// Diffie-Hellman exchange, so the client is verified against a counterparty rather than itself.
/// </summary>
public class OAuth1aAuthenticatorTests
{
    private static readonly Instant Start = Instant.FromUtc(2024, 5, 1, 12, 0, 0);
    private const string ConsumerKey = "TESTCONS";

    private static BigInteger Prime =>
        OAuth1aSignatures.ParseHex(
            Fixture.ReadJson("Authentication/oauth1a-vectors.json")
                .RootElement.GetProperty("diffieHellman")[0]
                .GetProperty("primeHex").GetString()!);

    /// <summary>Plays IBKR's side of the exchange.</summary>
    private sealed class FakeIbkr(RSA encryptionKey, byte[] secretPlaintext)
    {
        private readonly BigInteger _serverPrivate = OAuth1aSignatures.GeneratePrivateExponent();

        public string EncryptedAccessTokenSecret =>
            Convert.ToBase64String(encryptionKey.Encrypt(secretPlaintext, RSAEncryptionPadding.Pkcs1));

        public bool CorruptSignature { get; set; }

        public Instant ExpirationTimestamp { get; set; } = Start + Duration.FromMinutes(30);

        public HttpResponseMessage Respond(StubHttpMessageHandler.RecordedRequest request)
        {
            var challenge = ReadAuthorizationParameter(request, "diffie_hellman_challenge");
            var prime = Prime;

            var serverPublic = BigInteger.ModPow(2, _serverPrivate, prime);
            var shared = BigInteger.ModPow(OAuth1aSignatures.ParseHex(challenge), _serverPrivate, prime);

            var token = Convert.ToBase64String(
                HMACSHA1.HashData(OAuth1aSignatures.ToUnsignedBigEndianBytes(shared), secretPlaintext));

            var signature = Convert.ToHexStringLower(
                HMACSHA1.HashData(Convert.FromBase64String(token), Encoding.UTF8.GetBytes(ConsumerKey)));

            if (CorruptSignature)
            {
                signature = new string('0', signature.Length);
            }

            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["diffie_hellman_response"] = OAuth1aSignatures.ToHex(serverPublic),
                ["live_session_token_signature"] = signature,
                ["live_session_token_expiration"] = ExpirationTimestamp.ToUnixTimeMilliseconds(),
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }

        public string ExpectedToken(string challengeHex)
        {
            var shared = BigInteger.ModPow(OAuth1aSignatures.ParseHex(challengeHex), _serverPrivate, Prime);
            return Convert.ToBase64String(
                HMACSHA1.HashData(OAuth1aSignatures.ToUnsignedBigEndianBytes(shared), secretPlaintext));
        }
    }

    private static string ReadAuthorizationParameter(StubHttpMessageHandler.RecordedRequest request, string name)
    {
        var header = request.Headers["Authorization"];
        var marker = name + "=\"";
        var start = header.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = header.IndexOf('"', start);
        return header[start..end];
    }

    private static (OAuth1aAuthenticator Auth, StubHttpMessageHandler Stub, FakeIbkr Server, FakeClock Clock, IDisposable[] Keys)
        Build(Action<OAuth1aOptions>? configure = null)
    {
        var encryptionKey = RSA.Create(2048);
        var signatureKey = RSA.Create(2048);
        var secretPlaintext = RandomNumberGenerator.GetBytes(20);
        var server = new FakeIbkr(encryptionKey, secretPlaintext);

        var options = new OAuth1aOptions
        {
            ConsumerKey = ConsumerKey,
            Realm = OAuth1aOptions.TestRealm,
            AccessToken = "a1b2c3d4e5f60718293a",
            AccessTokenSecret = server.EncryptedAccessTokenSecret,
            DiffieHellmanPrime = Prime.ToString("x", CultureInfo.InvariantCulture).TrimStart('0'),
            DiffieHellmanGenerator = 2,
            CreateEncryptionKey = () => Clone(encryptionKey),
            CreateSignatureKey = () => Clone(signatureKey),
        };

        configure?.Invoke(options);

        var stub = new StubHttpMessageHandler();
        stub.AlwaysRespondWith(server.Respond);

        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };
        var clock = new FakeClock(Start);

        return (
            new OAuth1aAuthenticator(httpClient, Options.Create(options), clock),
            stub,
            server,
            clock,
            [encryptionKey, signatureKey]);

        static RSA Clone(RSA source)
        {
            var copy = RSA.Create();
            copy.ImportParameters(source.ExportParameters(includePrivateParameters: true));
            return copy;
        }
    }

    [Fact]
    public async Task Completes_the_handshake_and_signs_the_request_with_the_live_session_token()
    {
        var (auth, stub, server, _, keys) = Build();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("https://api.ibkr.com/v1/api/portfolio/accounts"));

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/oauth/live_session_token", stub.Requests[0].Path);

        var header = request.Headers.GetValues("Authorization").Single();
        Assert.StartsWith("OAuth ", header, StringComparison.Ordinal);
        Assert.Contains("oauth_signature_method=\"HMAC-SHA256\"", header, StringComparison.Ordinal);
        Assert.Contains("realm=\"test_realm\"", header, StringComparison.Ordinal);
        Assert.Contains("oauth_consumer_key=\"TESTCONS\"", header, StringComparison.Ordinal);

        // The signature must verify under the token the server independently derived.
        var challenge = ReadAuthorizationParameter(stub.Requests[0], "diffie_hellman_challenge");
        var expectedToken = server.ExpectedToken(challenge);
        var signature = Uri.UnescapeDataString(ReadAuthorizationParameter(
            new StubHttpMessageHandler.RecordedRequest(
                HttpMethod.Get,
                request.RequestUri!,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Authorization"] = header }),
            "oauth_signature"));

        var parameters = ParseOAuthParameters(header);
        parameters.Remove("oauth_signature");
        parameters.Remove("realm");
        var baseString = OAuth1aSignatures.BuildBaseString(
            null,
            "GET",
            "https://api.ibkr.com/v1/api/portfolio/accounts",
            parameters);

        Assert.Equal(OAuth1aSignatures.SignHmacSha256(baseString, expectedToken), signature);

        auth.Dispose();
        DisposeAll(keys);
    }

    [Fact]
    public async Task Signs_the_handshake_with_a_base_string_that_starts_with_the_prepend()
    {
        var (auth, stub, _, _, keys) = Build();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/x"));

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var header = stub.Requests[0].Headers["Authorization"];

        // The handshake header puts an unquoted realm first and declares RSA-SHA256.
        Assert.StartsWith("OAuth realm=test_realm, ", header, StringComparison.Ordinal);
        Assert.Contains("oauth_signature_method=\"RSA-SHA256\"", header, StringComparison.Ordinal);
        Assert.Contains("diffie_hellman_challenge=", header, StringComparison.Ordinal);

        auth.Dispose();
        DisposeAll(keys);
    }

    [Fact]
    public async Task Excludes_the_query_string_from_the_signature_base_string()
    {
        var (auth, stub, server, _, keys) = Build();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("https://api.ibkr.com/v1/api/iserver/account/trades?days=7"));

        await auth.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        var header = request.Headers.GetValues("Authorization").Single();
        var parameters = ParseOAuthParameters(header);
        var signature = Uri.UnescapeDataString(parameters["oauth_signature"]);
        parameters.Remove("oauth_signature");
        parameters.Remove("realm");

        var challenge = ReadAuthorizationParameter(stub.Requests[0], "diffie_hellman_challenge");
        var withoutQuery = OAuth1aSignatures.BuildBaseString(
            null,
            "GET",
            "https://api.ibkr.com/v1/api/iserver/account/trades",
            parameters);

        Assert.Equal(
            OAuth1aSignatures.SignHmacSha256(withoutQuery, server.ExpectedToken(challenge)),
            signature);

        auth.Dispose();
        DisposeAll(keys);
    }

    [Fact]
    public async Task Refuses_to_use_a_token_that_fails_ibkrs_signature_check()
    {
        var encryptionKey = RSA.Create(2048);
        var signatureKey = RSA.Create(2048);
        var secret = RandomNumberGenerator.GetBytes(20);
        var server = new FakeIbkr(encryptionKey, secret) { CorruptSignature = true };

        var options = new OAuth1aOptions
        {
            ConsumerKey = ConsumerKey,
            Realm = OAuth1aOptions.TestRealm,
            AccessToken = "token",
            AccessTokenSecret = server.EncryptedAccessTokenSecret,
            DiffieHellmanPrime = Prime.ToString("x", CultureInfo.InvariantCulture).TrimStart('0'),
            CreateEncryptionKey = () => CloneKey(encryptionKey),
            CreateSignatureKey = () => CloneKey(signatureKey),
        };

        var stub = new StubHttpMessageHandler();
        stub.AlwaysRespondWith(server.Respond);
        using var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.ibkr.com") };
        using var auth = new OAuth1aAuthenticator(httpClient, Options.Create(options), new FakeClock(Start));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/x"));

        var ex = await Assert.ThrowsAsync<IbkrAuthenticationException>(
            () => auth.AuthenticateAsync(request, TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("does not match the signature IBKR returned", ex.Message, StringComparison.Ordinal);
        Assert.False(request.Headers.Contains("Authorization"));

        encryptionKey.Dispose();
        signatureKey.Dispose();
    }

    [Fact]
    public async Task Reuses_the_live_session_token_until_it_expires()
    {
        var (auth, stub, _, clock, keys) = Build();

        using (var first = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/a")))
        {
            await auth.AuthenticateAsync(first, TestContext.Current.CancellationToken);
        }

        using (var second = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/b")))
        {
            await auth.AuthenticateAsync(second, TestContext.Current.CancellationToken);
        }

        Assert.Single(stub.Requests);

        // The fake server reports an expiry 30 minutes out; past that the handshake repeats.
        clock.Advance(Duration.FromMinutes(31));
        using (var third = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/c")))
        {
            await auth.AuthenticateAsync(third, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, stub.Requests.Count);

        auth.Dispose();
        DisposeAll(keys);
    }

    [Fact]
    public async Task Treats_a_near_present_expiration_field_as_the_computation_time()
    {
        // IBKR's endpoint reference describes this field as the moment of computation, with the
        // token valid for 24 hours from it. A value that is not comfortably in the future can only
        // mean that, so the handshake must not repeat immediately.
        var (auth, stub, server, clock, keys) = Build();
        server.ExpirationTimestamp = Start;

        using (var first = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/a")))
        {
            await auth.AuthenticateAsync(first, TestContext.Current.CancellationToken);
        }

        clock.Advance(Duration.FromHours(23));
        using (var second = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/b")))
        {
            await auth.AuthenticateAsync(second, TestContext.Current.CancellationToken);
        }

        Assert.Single(stub.Requests);

        clock.Advance(Duration.FromHours(2));
        using (var third = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ibkr.com/v1/api/c")))
        {
            await auth.AuthenticateAsync(third, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, stub.Requests.Count);

        auth.Dispose();
        DisposeAll(keys);
    }

    [Fact]
    public void Requires_both_keys_and_will_not_accept_one_for_the_other()
    {
        var options = new OAuth1aOptions
        {
            ConsumerKey = ConsumerKey,
            AccessToken = "token",
            AccessTokenSecret = "c2VjcmV0",
            DiffieHellmanPrime = "0d",
        };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("CreateEncryptionKey", ex.Message, StringComparison.Ordinal);
        Assert.Contains("distinct from the signing key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Requires_the_diffie_hellman_prime_issued_with_the_consumer_key()
    {
        var options = new OAuth1aOptions
        {
            ConsumerKey = ConsumerKey,
            AccessToken = "token",
            AccessTokenSecret = "c2VjcmV0",
        };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("DiffieHellmanPrime", ex.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ParseOAuthParameters(string header)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in header["OAuth ".Length..].Split(", ", StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            var key = part[..separator];
            var value = part[(separator + 1)..].Trim('"');
            result[key] = value;
        }

        return result;
    }

    private static RSA CloneKey(RSA source)
    {
        var copy = RSA.Create();
        copy.ImportParameters(source.ExportParameters(includePrivateParameters: true));
        return copy;
    }

    private static void DisposeAll(IDisposable[] disposables)
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }
}
