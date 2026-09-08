# Authentication

IBKR offers three ways in. They differ only in how a request is credentialed — resource paths and payloads are
identical — so switching mechanism is a configuration change and nothing else.

Getting in is the hard part, and it is not a code problem. The
[README's registration walkthrough](../../README.md#choosing-an-authentication-mechanism) is the account you want
before you write anything: who each mechanism is open to, which address to email, and what IBKR asks for. This
page is the other half — how the library performs each one, and what goes wrong.

| Mechanism | Who can use it | Setup |
| --- | --- | --- |
| Client Portal Gateway | Anyone with an IBKR Pro account | Self-serve |
| OAuth 2.0 | Organizations, Financial Advisors, IBrokers. **Not individuals** | Apply by email, then register a public key |
| OAuth 1.0a | Financial Advisors, organizations, third-party vendors | Apply by email, then a self-service portal |

If you are an individual trading your own account, the gateway is the path. OAuth 2.0 is closed to individual
account structures outright, and OAuth 1.0a's third-party route is IBKR's own estimate of two to three weeks of
vetting followed by three to six weeks of compliance and three to five weeks of legal.

## Client Portal Gateway

```csharp
services.AddIbkrTrading(options =>
{
    options.Environment = IbkrEnvironment.ClientPortalGateway;   // https://localhost:5000
})
.UseClientPortalGateway();
```

There is nothing to configure because there is nothing to sign. The gateway holds the session cookie from your
browser login and proxies requests on your behalf; `ClientPortalGatewayAuthenticator` adds no headers at all. Its
whole job is to be the thing that is not the other two.

What that buys in simplicity it costs in operations: a process has to be running and logged in, on a machine you
control, and nobody has logged into Trader Workstation with the same username since (see [Sessions](sessions.md)).

## OAuth 2.0

```csharp
services.AddIbkrTrading(options =>
{
    options.Environment = IbkrEnvironment.Production;   // https://api.ibkr.com, no gateway
})
.UseOAuth2(o =>
{
    o.ClientId        = "...";     // issued at registration
    o.ClientKeyId     = "...";     // identifies the registered public key
    o.Credential      = "...";     // the IBKR username the session is for
    o.ClientIpAddress = "...";     // your public egress address
    o.UsePrivateKeyFile("/secrets/ibkr-oauth2.pem");
});
```

The library signs an RS256 client-assertion JWT with the private half of the key you registered, exchanges it at
`/oauth2/api/v1/token` for an access token, and exchanges that at `/gw/api/v1/sso-sessions` for a session. The
access token is cached until 30 seconds before it expires (`AccessTokenRefreshSkew`) and the session for 24 hours
(`SessionTokenLifetime`); both are measured against the injected `IClock`, so a host can substitute a `FakeClock`
and test the refresh boundary without waiting.

**Send IBKR the public key only**, through the Secure Message Center, from a user on the live account:

```sh
openssl genrsa -out privatekey.pem 3072
openssl rsa -pubout -in privatekey.pem -out publickey.pem -outform PEM
```

Rotating the key changes the key ID and not the client ID.

`ClientIpAddress` is your public egress address and IBKR checks it. There is no way for the library to discover it
without asking a third party, which is not something a client should do behind your back — so it is required, and
`ClientIpAddressResolver` is there if you want to supply it from your own infrastructure metadata.

## OAuth 1.0a

```csharp
services.AddIbkrTrading(options =>
{
    options.Environment = IbkrEnvironment.Production;
})
.UseOAuth1a(o =>
{
    o.ConsumerKey        = "...";   // Self-Service Portal
    o.Realm              = OAuth1aOptions.LimitedPoaRealm;  // TestRealm for TESTCONS
    o.AccessToken        = "...";   // Self-Service Portal
    o.AccessTokenSecret  = "...";   // base64, still encrypted
    o.DiffieHellmanPrime = "...";   // hex, issued with the consumer key
    o.UseEncryptionKeyFile("/secrets/ibkr-encryption.pem"); // decrypts the token secret
    o.UseSignatureKeyFile("/secrets/ibkr-signature.pem");   // signs the handshake
});
```

The library performs the whole handshake on first use — RSA-decrypting the access token secret, running the
Diffie-Hellman exchange, deriving the live session token and validating it — then signs each request with the
resulting token, renewing it when the expiry IBKR returned passes. IBKR documents that as 24 hours;
`LiveSessionToken.ResolveExpiry` reads it from the value rather than assuming, because IBKR has sent both an
absolute expiry and a computation timestamp in the same field.

**The two keys are different keys.** The encryption key decrypts the access token secret; the signature key signs
the handshake. Swapping them fails at the point of signing, with an error that does not say which one is wrong.

The Diffie-Hellman generator is fixed at 2 and the prime is read as hex, with or without a `0x` prefix.

> **A newly registered consumer key does not work until after midnight** — New York, Zug or Hong Kong, whichever
> region you are in. Used before that reset it returns `401 Invalid Consumer`, which looks exactly like a signing
> bug and is not one. This is the single most expensive thing on this page to not know.

## Where credentials should live

Both OAuth mechanisms take a private key. `UsePrivateKeyFile`, `UseEncryptionKeyFile` and `UseSignatureKeyFile`
read PEM from a path, which is what a secret mounted into a container looks like. `UsePrivateKeyPem`,
`UseEncryptionKeyPem` and `UseSignatureKeyPem` take the PEM itself, for a host that fetches from a vault; and
`CreatePrivateKey`, `CreateEncryptionKey` and `CreateSignatureKey` take an `RSA` factory, for a key that never
leaves an HSM as text.

Nothing in this library logs a credential, a key, a token or an account identifier. The live sweep in
`samples/IbkrDotNet.Samples.Verify` masks the account number it discovers at runtime, so its output can go
straight into a bug report.

## Choosing at runtime

`UseClientPortalGateway`, `UseOAuth2` and `UseOAuth1a` each replace the registered `IIbkrAuthenticator`, so the
last one wins and a host can pick from configuration:

```csharp
var ibkr = services.AddIbkrTrading(configuration.GetSection("Ibkr"));

if (configuration["Ibkr:Auth"] == "oauth2")
    ibkr.UseOAuth2(o => configuration.GetSection("Ibkr:OAuth2").Bind(o));
else
    ibkr.UseClientPortalGateway();
```

The endpoint clients neither know nor care which one was chosen.
