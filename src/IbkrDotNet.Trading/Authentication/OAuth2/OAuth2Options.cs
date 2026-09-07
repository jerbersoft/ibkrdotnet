using System.Security.Cryptography;
using NodaTime;

namespace IbkrDotNet.Trading.Authentication.OAuth2;

/// <summary>
/// Configures OAuth 2.0 authentication against Interactive Brokers.
/// </summary>
/// <remarks>
/// Available to licensed Organizations, Financial Advisors and IBrokers. Credentials are issued
/// during IBKR's registration process.
/// </remarks>
public sealed class OAuth2Options
{
    /// <summary>The client identifier issued by IBKR at registration.</summary>
    /// <remarks>Used as both <c>iss</c> and <c>sub</c> in the access-token assertion.</remarks>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The key identifier telling IBKR which registered public key verifies the assertion signature.
    /// Sent as the JWT header's <c>kid</c>.
    /// </summary>
    public string ClientKeyId { get; set; } = string.Empty;

    /// <summary>The IBKR username the brokerage session is established for.</summary>
    public string Credential { get; set; } = string.Empty;

    /// <summary>The scope requested for the access token.</summary>
    public string Scope { get; set; } = "sso-sessions.write";

    /// <summary>
    /// The public IP address the requests originate from.
    /// </summary>
    /// <remarks>
    /// IBKR validates this claim against the address the request actually arrives from, so it must
    /// be accurate and current. It has no default: IBKR's reference implementation discovers it by
    /// calling a third-party address-lookup service, and this library will not make an outbound
    /// request to an unrelated host on a caller's behalf without being asked. Set this explicitly,
    /// or supply <see cref="ClientIpAddressResolver"/>.
    /// </remarks>
    public string? ClientIpAddress { get; set; }

    /// <summary>
    /// Resolves the public IP address when <see cref="ClientIpAddress"/> is not set, for deployments
    /// where the address is not known up front.
    /// </summary>
    public Func<CancellationToken, ValueTask<string>>? ClientIpAddressResolver { get; set; }

    /// <summary>
    /// Creates the RSA private key used to sign assertions. The returned key is disposed after use,
    /// so this is called once per signing operation.
    /// </summary>
    /// <remarks>
    /// Set it directly to source the key from a key vault or HSM, or call
    /// <see cref="UsePrivateKeyPem"/> / <see cref="UsePrivateKeyFile"/> for a PEM on disk.
    /// </remarks>
    public Func<RSA>? CreatePrivateKey { get; set; }

    /// <summary>
    /// How early an access token is treated as expired, to tolerate clock skew and request latency.
    /// </summary>
    public Duration AccessTokenRefreshSkew { get; set; } = Duration.FromSeconds(30);

    /// <summary>
    /// How long an established SSO session token is reused before a new one is requested.
    /// </summary>
    /// <remarks>
    /// IBKR's own assertion for this step is issued with a 24-hour expiry, which is the ceiling this
    /// default follows. Within that window the brokerage session is kept alive by <c>/tickle</c>.
    /// </remarks>
    public Duration SessionTokenLifetime { get; set; } = Duration.FromHours(24);

    /// <summary>Signs assertions with an RSA private key in PEM form.</summary>
    /// <param name="pem">The PEM-encoded private key.</param>
    public OAuth2Options UsePrivateKeyPem(string pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        CreatePrivateKey = () =>
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
        };

        return this;
    }

    /// <summary>Signs assertions with an RSA private key read from a PEM file.</summary>
    /// <param name="path">The path to the PEM file.</param>
    /// <remarks>The file is read on each signing operation, so rotating the key on disk takes effect.</remarks>
    public OAuth2Options UsePrivateKeyFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CreatePrivateKey = () =>
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(File.ReadAllText(path));
                return rsa;
            }
            catch
            {
                rsa.Dispose();
                throw;
            }
        };

        return this;
    }

    /// <summary>Throws when the options are not usable.</summary>
    /// <exception cref="InvalidOperationException">A required value is missing.</exception>
    public void Validate()
    {
        Require(ClientId, nameof(ClientId));
        Require(ClientKeyId, nameof(ClientKeyId));
        Require(Credential, nameof(Credential));
        Require(Scope, nameof(Scope));

        if (CreatePrivateKey is null)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth2Options)}.{nameof(CreatePrivateKey)} is not set. Call " +
                $"{nameof(UsePrivateKeyPem)} or {nameof(UsePrivateKeyFile)}, or assign a factory " +
                "that returns the RSA key registered with IBKR.");
        }

        if (string.IsNullOrWhiteSpace(ClientIpAddress) && ClientIpAddressResolver is null)
        {
            throw new InvalidOperationException(
                $"{nameof(OAuth2Options)}.{nameof(ClientIpAddress)} is not set. IBKR validates this " +
                "claim against the address the request arrives from, so it must be supplied " +
                $"explicitly or discovered through {nameof(ClientIpAddressResolver)}.");
        }

        static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{nameof(OAuth2Options)}.{name} is required for OAuth 2.0 authentication.");
            }
        }
    }
}
