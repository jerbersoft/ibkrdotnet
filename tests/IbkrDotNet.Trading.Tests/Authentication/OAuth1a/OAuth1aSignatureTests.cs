using System.Numerics;
using System.Text.Json;
using IbkrDotNet.Trading.Authentication.OAuth1a;
using IbkrDotNet.Trading.Tests.TestSupport;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Authentication.OAuth1a;

/// <summary>
/// Checks the OAuth 1.0a primitives against vectors produced by a faithful transcription of IBKR's
/// published reference implementation, rather than only against themselves.
/// </summary>
/// <remarks>
/// The derivation has several places where a single stray byte yields a valid-looking token that
/// IBKR silently rejects — the sign-bit padding of the shared secret above all — so agreement with
/// an independent implementation is the property worth testing.
/// </remarks>
public class OAuth1aSignatureTests
{
    private static JsonElement Vectors =>
        Fixture.ReadJson("Authentication/oauth1a-vectors.json").RootElement.Clone();

    public static TheoryData<string> DiffieHellmanCaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var vector in Vectors.GetProperty("diffieHellman").EnumerateArray())
        {
            data.Add(vector.GetProperty("name").GetString()!);
        }

        return data;
    }

    private static JsonElement Case(string name) =>
        Vectors.GetProperty("diffieHellman")
            .EnumerateArray()
            .First(v => v.GetProperty("name").GetString() == name);

    [Theory]
    [MemberData(nameof(DiffieHellmanCaseNames))]
    public void Computes_the_same_challenge_as_the_reference_implementation(string name)
    {
        var vector = Case(name);
        var prime = OAuth1aSignatures.ParseHex(vector.GetProperty("primeHex").GetString()!);
        var privateExponent = OAuth1aSignatures.ParseHex(vector.GetProperty("clientPrivateHex").GetString()!);

        var challenge = OAuth1aSignatures.ComputeChallenge(
            vector.GetProperty("generator").GetInt32(),
            privateExponent,
            prime);

        Assert.Equal(vector.GetProperty("clientChallengeHex").GetString(), challenge);
    }

    [Theory]
    [MemberData(nameof(DiffieHellmanCaseNames))]
    public void Derives_the_same_live_session_token_as_the_reference_implementation(string name)
    {
        var vector = Case(name);

        var token = OAuth1aSignatures.DeriveLiveSessionToken(
            vector.GetProperty("serverResponseHex").GetString()!,
            OAuth1aSignatures.ParseHex(vector.GetProperty("clientPrivateHex").GetString()!),
            OAuth1aSignatures.ParseHex(vector.GetProperty("primeHex").GetString()!),
            Convert.FromHexString(vector.GetProperty("prependHex").GetString()!));

        Assert.Equal(vector.GetProperty("liveSessionToken").GetString(), token);
    }

    [Fact]
    public void Covers_the_sign_bit_padding_case_where_the_shared_secret_fills_whole_bytes()
    {
        // The padded vector exists precisely because this is the encoding detail that diverges
        // silently between big-integer implementations.
        var vector = Case("signBitPadded");

        Assert.Equal(0, vector.GetProperty("sharedSecretBitLength").GetInt32() % 8);
    }

    [Fact]
    public void Prepends_a_zero_byte_only_when_the_bit_length_is_a_multiple_of_eight()
    {
        // 0xFF: 8 bits, an exact multiple, so it gains a leading zero byte.
        Assert.Equal(
            new byte[] { 0x00, 0xFF },
            OAuth1aSignatures.ToUnsignedBigEndianBytes(new BigInteger(255)));

        // 0x7F: 7 bits, so it does not.
        Assert.Equal(
            new byte[] { 0x7F },
            OAuth1aSignatures.ToUnsignedBigEndianBytes(new BigInteger(127)));
    }

    [Theory]
    [MemberData(nameof(DiffieHellmanCaseNames))]
    public void Verifies_a_correctly_derived_token_against_ibkrs_signature(string name)
    {
        var vector = Case(name);

        Assert.True(OAuth1aSignatures.VerifyLiveSessionToken(
            vector.GetProperty("liveSessionToken").GetString()!,
            vector.GetProperty("consumerKey").GetString()!,
            vector.GetProperty("liveSessionTokenSignature").GetString()!));
    }

    [Fact]
    public void Rejects_a_token_that_does_not_match_ibkrs_signature()
    {
        var vector = Case("ordinary");

        Assert.False(OAuth1aSignatures.VerifyLiveSessionToken(
            Convert.ToBase64String(new byte[20]),
            vector.GetProperty("consumerKey").GetString()!,
            vector.GetProperty("liveSessionTokenSignature").GetString()!));
    }

    [Fact]
    public void Percent_encodes_exactly_as_the_reference_implementation_does()
    {
        foreach (var pair in Vectors.GetProperty("percentEncoding").EnumerateObject())
        {
            Assert.Equal(pair.Value.GetString(), OAuth1aSignatures.PercentEncode(pair.Name));
        }
    }

    [Fact]
    public void Builds_the_same_signature_base_string_as_the_reference_implementation()
    {
        var expected = Vectors.GetProperty("baseString");
        var parameters = expected.GetProperty("params")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);

        var baseString = OAuth1aSignatures.BuildBaseString(
            prepend: null,
            expected.GetProperty("method").GetString()!,
            expected.GetProperty("url").GetString()!,
            parameters);

        Assert.Equal(expected.GetProperty("normalizedParams").GetString(),
            OAuth1aSignatures.NormalizeParameters(parameters));
        Assert.Equal(expected.GetProperty("expected").GetString(), baseString);
    }

    [Fact]
    public void Concatenates_the_prepend_onto_the_front_without_encoding_or_a_separator()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1" };

        var baseString = OAuth1aSignatures.BuildBaseString("deadbeef", "POST", "https://api.ibkr.com/x", parameters);

        // The prepend is unique to IBKR's flow: no '&' separator, and not URL-encoded.
        Assert.StartsWith("deadbeefPOST&", baseString, StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_the_authenticated_header_with_realm_quoted_and_sorted()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_token"] = "tok",
            ["oauth_consumer_key"] = "TESTCONS",
            ["realm"] = "limited_poa",
        };

        Assert.Equal(
            """OAuth oauth_consumer_key="TESTCONS", oauth_token="tok", realm="limited_poa" """.TrimEnd(),
            OAuth1aSignatures.BuildAuthorizationHeader(parameters));
    }

    [Fact]
    public void Renders_the_handshake_header_with_realm_unquoted_and_leading()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_token"] = "tok",
            ["oauth_consumer_key"] = "TESTCONS",
        };

        // The handshake header shapes 'realm' differently from every later request.
        Assert.Equal(
            """OAuth realm=test_realm, oauth_consumer_key="TESTCONS", oauth_token="tok" """.TrimEnd(),
            OAuth1aSignatures.BuildLiveSessionTokenAuthorizationHeader("test_realm", parameters));
    }
}
