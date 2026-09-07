using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace IbkrDotNet.Trading.Authentication.OAuth1a;

/// <summary>
/// The signing and key-derivation primitives of IBKR's OAuth 1.0a flow, as pure functions.
/// </summary>
/// <remarks>
/// Kept free of I/O so each step can be checked against IBKR's documented worked example. Every one
/// of these is a place where a single stray byte produces a valid-looking value that the server
/// silently rejects, so they are separated from the request plumbing on purpose.
/// </remarks>
internal static class OAuth1aSignatures
{
    /// <summary>
    /// Percent-encodes a value the way OAuth 1.0a requires: everything outside the unreserved set
    /// <c>A-Z a-z 0-9 - . _ ~</c> becomes <c>%XX</c> with uppercase hexadecimal.
    /// </summary>
    public static string PercentEncode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length * 3);

        foreach (var b in bytes)
        {
            if (IsUnreserved((char)b))
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();

        static bool IsUnreserved(char c) =>
            char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
    }

    /// <summary>
    /// Joins OAuth parameters into the normalized <c>key=value&amp;key=value</c> string, sorted by
    /// key.
    /// </summary>
    public static string NormalizeParameters(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return string.Join(
            '&',
            parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
    }

    /// <summary>
    /// Builds the signature base string <c>{prepend}METHOD&amp;URL&amp;PARAMS</c>.
    /// </summary>
    /// <param name="prepend">
    /// The hex-encoded decrypted access token secret, used only by the live session token request.
    /// It is concatenated directly onto the front, neither URL-encoded nor separated by an
    /// <c>&amp;</c>; that structural quirk is unique to IBKR's flow.
    /// </param>
    /// <param name="method">The HTTP method, uppercase.</param>
    /// <param name="url">The request URL without its query string.</param>
    /// <param name="parameters">The OAuth parameters to normalize.</param>
    public static string BuildBaseString(
        string? prepend,
        string method,
        string url,
        IReadOnlyDictionary<string, string> parameters) =>
        $"{prepend}{method}&{PercentEncode(url)}&{PercentEncode(NormalizeParameters(parameters))}";

    /// <summary>
    /// Renders the <c>Authorization</c> header for an authenticated request, with <c>realm</c>
    /// quoted and sorted among the OAuth parameters.
    /// </summary>
    public static string BuildAuthorizationHeader(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var rendered = parameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}=\"{p.Value}\"");

        return "OAuth " + string.Join(", ", rendered);
    }

    /// <summary>
    /// Renders the <c>Authorization</c> header for the live session token request, where
    /// <c>realm</c> is unquoted and leads the parameter list.
    /// </summary>
    public static string BuildLiveSessionTokenAuthorizationHeader(
        string realm,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var rendered = parameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}=\"{p.Value}\"");

        return $"OAuth realm={realm}, " + string.Join(", ", rendered);
    }

    /// <summary>Signs a base string with RSA-SHA256 and PKCS#1 v1.5 padding, returning base64.</summary>
    public static string SignRsaSha256(string baseString, RSA signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        var signature = signingKey.SignData(
            Encoding.UTF8.GetBytes(baseString),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Signs a base string with HMAC-SHA256 keyed by the live session token, returning base64.
    /// </summary>
    /// <remarks>
    /// This is the dividing line between the handshake and ongoing operation: the handshake signs
    /// with the application's RSA key, while every authenticated call afterwards signs symmetrically
    /// with the token both sides now share.
    /// </remarks>
    public static string SignHmacSha256(string baseString, string liveSessionToken)
    {
        var key = Convert.FromBase64String(liveSessionToken);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(baseString));
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Decrypts the base64-encoded access token secret with RSA PKCS#1 v1.5 <em>encryption</em>
    /// padding, which is a different padding mode from the signature padding used elsewhere.
    /// </summary>
    public static byte[] DecryptAccessTokenSecret(string accessTokenSecret, RSA encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);
        return encryptionKey.Decrypt(Convert.FromBase64String(accessTokenSecret), RSAEncryptionPadding.Pkcs1);
    }

    /// <summary>Computes the client's public Diffie-Hellman value, hex-encoded for transport.</summary>
    public static string ComputeChallenge(BigInteger generator, BigInteger privateExponent, BigInteger prime) =>
        ToHex(BigInteger.ModPow(generator, privateExponent, prime));

    /// <summary>Generates the client's secret 256-bit Diffie-Hellman exponent.</summary>
    /// <remarks>This value is never transmitted and must not be logged.</remarks>
    public static BigInteger GeneratePrivateExponent()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Derives the live session token from the Diffie-Hellman exchange.
    /// </summary>
    /// <param name="serverResponseHex">IBKR's public Diffie-Hellman value, hex-encoded.</param>
    /// <param name="privateExponent">The client's secret exponent.</param>
    /// <param name="prime">The shared Diffie-Hellman prime.</param>
    /// <param name="prependBytes">The decrypted access token secret bytes.</param>
    /// <returns>The base64-encoded live session token.</returns>
    /// <remarks>
    /// This is the only place in the entire flow that uses SHA-1; every RSA signature uses SHA-256.
    /// </remarks>
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "HMAC-SHA1 is fixed by IBKR's OAuth 1.0a protocol: the server derives the live session token the same way, so a stronger hash would produce a token IBKR rejects. It is used only for this key derivation and its verification, never for RSA signatures, which use SHA-256 throughout.")]
    public static string DeriveLiveSessionToken(
        string serverResponseHex,
        BigInteger privateExponent,
        BigInteger prime,
        byte[] prependBytes)
    {
        var serverPublic = ParseHex(serverResponseHex);
        var shared = BigInteger.ModPow(serverPublic, privateExponent, prime);
        var sharedBytes = ToUnsignedBigEndianBytes(shared);
        return Convert.ToBase64String(HMACSHA1.HashData(sharedBytes, prependBytes));
    }

    /// <summary>
    /// Checks a derived live session token against the signature IBKR returned alongside its
    /// Diffie-Hellman response.
    /// </summary>
    /// <remarks>
    /// A match confirms the shared secret, the decryption of the access token secret and every
    /// byte-encoding edge case all agree with IBKR's server. This is the only gate on trusting the
    /// derived token, so a mismatch must abort rather than proceed.
    /// </remarks>
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "HMAC-SHA1 is fixed by IBKR's OAuth 1.0a protocol: the server derives the live session token the same way, so a stronger hash would produce a token IBKR rejects. It is used only for this key derivation and its verification, never for RSA signatures, which use SHA-256 throughout.")]
    public static bool VerifyLiveSessionToken(string liveSessionToken, string consumerKey, string expectedSignature)
    {
        var key = Convert.FromBase64String(liveSessionToken);
        var actual = Convert.ToHexStringLower(HMACSHA1.HashData(key, Encoding.UTF8.GetBytes(consumerKey)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual),
            Encoding.ASCII.GetBytes(expectedSignature?.Trim().ToLowerInvariant() ?? string.Empty));
    }

    /// <summary>
    /// Renders a positive integer as the unsigned big-endian bytes IBKR's server expects.
    /// </summary>
    /// <remarks>
    /// When the value's bit length is an exact multiple of eight, a leading zero byte is prepended.
    /// IBKR's server derives the same bytes from a big-integer implementation that always carries a
    /// sign bit, so without this the two sides produce different HMAC keys and the derived token is
    /// silently rejected.
    /// </remarks>
    internal static byte[] ToUnsignedBigEndianBytes(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (value.GetBitLength() % 8 != 0)
        {
            return bytes;
        }

        var padded = new byte[bytes.Length + 1];
        bytes.CopyTo(padded, 1);
        return padded;
    }

    /// <summary>Renders a positive integer as lowercase hexadecimal with no leading zero digit.</summary>
    internal static string ToHex(BigInteger value)
    {
        var hex = Convert.ToHexStringLower(value.ToByteArray(isUnsigned: true, isBigEndian: true));
        return hex.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";
    }

    /// <summary>Parses a hexadecimal string into a positive integer.</summary>
    internal static BigInteger ParseHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        var text = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

        // A leading zero digit keeps the high bit from being read as a sign bit.
        return BigInteger.Parse("0" + text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
