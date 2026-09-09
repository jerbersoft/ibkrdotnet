# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Report it privately through GitHub's private vulnerability reporting:

**[Report a vulnerability →](https://github.com/jerbersoft/ibkrdotnet/security/advisories/new)**

That opens a private advisory visible only to you and the maintainer, and it can become a published advisory and
a CVE once a fix is out.

If that page is unavailable to you, open a regular issue containing **no detail** — just a request for a private
channel — and you will be contacted.

### What to include

* The affected version — a commit SHA, since nothing is on nuget.org yet.
* Which authentication mechanism is involved, if any: Client Portal Gateway, OAuth 1.0a, or OAuth 2.0.
* What an attacker can do, and what they need in order to do it.
* A minimal reproduction if you have one.

### Never include real credentials

This is a brokerage client. The material it handles is worth more than most:

* **RSA private keys.** Both OAuth mechanisms take one. A key in a bug report is a compromised key — rotate it
  with IBKR rather than sending it.
* **Live session tokens, access tokens and access-token secrets.** OAuth 1.0a derives a live session token that
  signs every request; OAuth 2.0 holds a bearer token. Either is enough to trade.
* **Account numbers.** `U`- and `DU`-prefixed identifiers are not secrets by themselves, but they identify you to
  IBKR support and to anyone reading the report. Redact them to `U1234567`, as the test fixtures do.

Exception messages and request renderings are safe to attach as they stand. `IbkrRequest` carries no header,
token or credential members at all — authentication is applied by a `DelegatingHandler` further down the stack —
and `IbkrRequest.ToString()` renders only the method and the relative URI. **A path that puts credential material
into a message, a log or a rendered request is a vulnerability**, and worth reporting as one.

## Supported versions

| Version | Supported |
| --- | --- |
| The tip of `master` | ✅ |
| Anything older | ❌ — fixes land on `master` |

**This project is in development and publishes no package yet.** There is no release stream to back-port to, so
the supported version is whatever `master` currently is. That changes when the first version reaches nuget.org.

## Scope

This is a client library. It makes outbound HTTPS requests to Interactive Brokers, holds credentials in memory,
signs requests, and deserializes responses. **In scope:**

* **Credential disclosure.** Any path that leaks a private key, an access token, an access-token secret or a live
  session token — into an exception message, a log line, a rendered request, a URL, or a crash dump.
* **Flaws in the OAuth 1.0a implementation.** The Diffie-Hellman exchange, the RSA decryption of the
  access-token secret, the live-session-token derivation, the LST signature check, and the HMAC-SHA256 request
  signature. A defect here can produce a signature an attacker can forge or replay, and it is the least
  reviewable code in the repository.
* **Deserialization flaws** reachable from a response body.
* **Path or query injection** through a caller-supplied account id, conid, symbol or parameter.
* **Denial of service against the consuming host** — unbounded memory on a large response, or an upstream value
  that stalls the process. Server-supplied `Retry-After` is bounded by `MaxWait` for exactly this reason.
* **Rate-limit evasion that costs the caller.** IBKR answers a breach by putting the calling IP in a ten-minute
  penalty box, and blocks repeat offenders. A defect that makes the client exceed a published limit has a real
  cost, so it is treated as a security issue rather than a bug.

**Out of scope**, though still worth an ordinary issue:

* Vulnerabilities in IBKR's own API, the Client Portal Gateway, or Trader Workstation — report those to IBKR.
* Behaviour of a dependency, unless this library's use of it is what creates the problem.
* Anything requiring the attacker to already control the configuration or the process.
* The gateway's self-signed certificate. `TrustGatewayCertificate` exists to accept it against a local gateway
  and is documented as a development affordance; using it against a remote host is a configuration mistake, not
  a library defect.

## Handling your own credentials

Not a vulnerability class, but the most likely way an incident actually happens:

* **Send IBKR the public key only.** `openssl genrsa` writes the private key into your working directory. This
  repository git-ignores `*.pem`, `*.key`, `.env` and friends, but that protects this tree — not the one you run
  the command in. Generate keys outside any repository.
* **The private key never needs to travel.** `UsePrivateKeyFile`, `UseEncryptionKeyFile` and
  `UseSignatureKeyFile` read from disk at startup; nothing uploads them.
* **In CI, use a secret**, never a checked-in file.
* **A brokerage session is exclusive.** A username holds one at a time across every platform, so a session
  established here displaces the one in Trader Workstation, and vice versa. That is IBKR's behaviour, not a
  defect — but it means credentials shared between people interrupt each other's trading.
