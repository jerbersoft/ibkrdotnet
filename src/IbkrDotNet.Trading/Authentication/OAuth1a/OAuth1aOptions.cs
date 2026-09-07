using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace IbkrDotNet.Trading.Authentication.OAuth1a;

/// <summary>
/// Configures OAuth 1.0a authentication against Interactive Brokers.
/// </summary>
/// <remarks>
/// Available to licensed Financial Advisors, organizations and third-party service providers. All
/// values here are issued during IBKR's registration process.
/// </remarks>
public sealed class OAuth1aOptions
{
    /// <summary>The realm used for TESTCONS, IBKR's test consumer key.</summary>
    public const string TestRealm = "test_realm";

    /// <summary>The realm used for every consumer key other than TESTCONS.</summary>
    public const string LimitedPoaRealm = "limited_poa";

    /// <summary>The consumer key issued by IBKR at registration.</summary>
    public string ConsumerKey { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth realm. Use <see cref="TestRealm"/> with the TESTCONS consumer key and
    /// <see cref="LimitedPoaRealm"/> otherwise.
    /// </summary>
    public string Realm { get; set; } = LimitedPoaRealm;

    /// <summary>
    /// The permanent access token, from <c>/oauth/access_token</c> or IBKR's Self Service Portal.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The access token secret, base64-encoded and RSA-encrypted, as IBKR issues it.
    /// </summary>
    /// <remarks>
    /// It is decrypted locally with <see cref="CreateEncryptionKey"/> to produce the <c>prepend</c>
    /// that both prefixes the handshake's signature base string and is the message the live session
    /// token is derived from.
    /// </remarks>
    public string AccessTokenSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Diffie-Hellman prime issued with the consumer key, as a hexadecimal string.
    /// </summary>
    /// <remarks>
    /// It must match IBKR's server-side value exactly; the shared secret cannot be derived otherwise.
    /// </remarks>
    public string DiffieHellmanPrime { get; set; } = string.Empty;

    /// <summary>The Diffie-Hellman generator. IBKR fixes this at 2.</summary>
    public int DiffieHellmanGenerator { get; set; } = 2;

    /// <summary>
    /// Creates the RSA private <em>encryption</em> key, used to decrypt the access token secret.
    /// </summary>
    /// <remarks>
    /// This is a different key from <see cref="CreateSignatureKey"/>. The returned key is disposed
    /// after use.
    /// </remarks>
    public Func<RSA>? CreateEncryptionKey { get; set; }

    /// <summary>
    /// Creates the RSA private <em>signing</em> key, used to sign the handshake's base string.
    /// </summary>
    /// <remarks>
    /// This is a different key from <see cref="CreateEncryptionKey"/>. The returned key is disposed
    /// after use.
    /// </remarks>
    public Func<RSA>? CreateSignatureKey { get; set; }

    /// <summary>Sets the RSA private encryption key from PEM text.</summary>
    /// <param name="pem">The PEM-encoded private key.</param>
    public OAuth1aOptions UseEncryptionKeyPem(string pem)
    {
        CreateEncryptionKey = RsaKeys.FromPem(pem);
        return this;
    }

    /// <summary>Sets the RSA private encryption key from a PEM file.</summary>
    /// <param name="path">The path to the PEM file.</param>
    public OAuth1aOptions UseEncryptionKeyFile(string path)
    {
        CreateEncryptionKey = RsaKeys.FromPemFile(path);
        return this;
    }

    /// <summary>Sets the RSA private signing key from PEM text.</summary>
    /// <param name="pem">The PEM-encoded private key.</param>
    public OAuth1aOptions UseSignatureKeyPem(string pem)
    {
        CreateSignatureKey = RsaKeys.FromPem(pem);
        return this;
    }

    /// <summary>Sets the RSA private signing key from a PEM file.</summary>
    /// <param name="path">The path to the PEM file.</param>
    public OAuth1aOptions UseSignatureKeyFile(string path)
    {
        CreateSignatureKey = RsaKeys.FromPemFile(path);
        return this;
    }

    /// <summary>Parses <see cref="DiffieHellmanPrime"/> into a positive integer.</summary>
    /// <exception cref="InvalidOperationException">The prime is missing or not valid hexadecimal.</exception>
    public BigInteger ParsePrime()
    {
        if (string.IsNullOrWhiteSpace(DiffieHellmanPrime))
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth1aOptions)}.{nameof(DiffieHellmanPrime)} is required; IBKR issues it " +
                "alongside the consumer key.");
        }

        var hex = DiffieHellmanPrime.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? DiffieHellmanPrime[2..]
            : DiffieHellmanPrime;

        // A leading zero digit keeps BigInteger from reading the high bit as a sign bit.
        if (!BigInteger.TryParse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var prime) ||
            prime <= BigInteger.One)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth1aOptions)}.{nameof(DiffieHellmanPrime)} is not a valid hexadecimal prime.");
        }

        return prime;
    }

    /// <summary>Throws when the options are not usable.</summary>
    /// <exception cref="InvalidOperationException">A required value is missing.</exception>
    public void Validate()
    {
        Require(ConsumerKey, nameof(ConsumerKey));
        Require(Realm, nameof(Realm));
        Require(AccessToken, nameof(AccessToken));
        Require(AccessTokenSecret, nameof(AccessTokenSecret));

        _ = ParsePrime();

        if (DiffieHellmanGenerator < 2)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth1aOptions)}.{nameof(DiffieHellmanGenerator)} must be at least 2; IBKR fixes it at 2.");
        }

        if (CreateEncryptionKey is null)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth1aOptions)}.{nameof(CreateEncryptionKey)} is not set. This is the RSA " +
                "key that decrypts the access token secret, and is distinct from the signing key.");
        }

        if (CreateSignatureKey is null)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth1aOptions)}.{nameof(CreateSignatureKey)} is not set. This is the RSA " +
                "key that signs the handshake base string, and is distinct from the encryption key.");
        }

        static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{nameof(OAuth1aOptions)}.{name} is required for OAuth 1.0a authentication.");
            }
        }
    }
}

internal static class RsaKeys
{
    public static Func<RSA> FromPem(string pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        return () => Import(pem);
    }

    public static Func<RSA> FromPemFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return () => Import(File.ReadAllText(path));
    }

    private static RSA Import(string pem)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}
