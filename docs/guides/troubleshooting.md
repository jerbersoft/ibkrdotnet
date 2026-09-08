# Troubleshooting

Most of what goes wrong here is IBKR behaving as designed in a way that looks exactly like a bug in your code.
This page is the list, ordered by how much time each one costs before you find it.

## `401 Invalid Consumer` on a brand new OAuth 1.0a key

**It is the midnight reset.** A consumer key registered in the Self-Service Portal does not work until after
midnight in New York, Zug or Hong Kong, whichever region you are in. Before then it returns `401 Invalid
Consumer`, which is indistinguishable from a signing bug.

Nothing you change in the code will fix it. Wait, then try the identical request.

## Everything returns `401` and the credentials are right

On an `/iserver` endpoint this is usually a lapsed brokerage session rather than bad credentials. Call
`EnsureBrokerageSessionAsync` and retry.

If it keeps happening overnight: a session expires outright at midnight regardless of the keep-alive.
[Sessions](sessions.md) has the detail.

## The market data snapshot is empty

Expected on the first call. IBKR treats it as a pre-flight that starts the backend streaming; ask again.

If it is *still* empty after several calls, you may have exhausted your market data lines (100 by default) by
subscribing without unsubscribing. `UnsubscribeAllAsync` releases them. See [Market data](market-data.md).

## A `/portfolio` endpoint returns nothing for an account that has positions

Call `GetAccountsAsync` first. Other `/portfolio` endpoints return empty or stale data for an account that has not
been listed, and IBKR reports no error when they do.

## Requests succeed but the account looks empty

Check `Established`, not `Authenticated`. A session that is connected and authenticated but not yet initialized
answers account requests empty rather than failing.

```csharp
var status = await ibkr.Session.EnsureBrokerageSessionAsync(ct);
if (!status.Established) { /* not ready */ }
```

## Your session keeps getting dropped

`Competing` on the session status means another login is holding the same username — Trader Workstation, Client
Portal, or another instance of your own service. IBKR allows one brokerage session per username across all
platforms. The fix is a second username, not a retry loop.

## The gateway will not start on macOS

Port 5000 is held by the AirPlay receiver. Change `listenPort` on line 4 of `root/conf.yaml` — 5001 is the
conventional alternative — and point the client at it:

```csharp
options.BaseAddress = new Uri("https://localhost:5001");
```

It is a file setting, not a command-line flag.

## Certificate errors against the gateway

The gateway serves a self-signed certificate on loopback. Browsers warn; `HttpClient` refuses outright. The
samples take `--TrustGatewayCertificate=true`, which relaxes validation **for loopback addresses only** so it
cannot quietly disable it against a real IBKR host. In your own application, trust the gateway's certificate
explicitly rather than disabling validation globally.

## You logged in but reached the wrong account

The gateway login page has no live/paper switch. Which account you reach is decided by the username you type, and
paper trading needs a separate paper username.

## `IbkrRateLimitExceededException` before any request was sent

The client refuses a wait longer than `RateLimiting.MaxWait` (30 seconds) rather than blocking silently — several
endpoints allow one request per fifteen minutes, and a silent block that long is indistinguishable from a hang.
`ex.Limit` names which limit and `ex.RetryAfter` says how long. Raise `MaxWait` if you would rather wait.

## `429` from IBKR despite the client pacing

Possible, and the reason the client feeds it back. Parts of the limits table are inference — `/pa/allperiods` is
in no IBKR limits table at all. It can also mean a second process is using the same username, or you share an
egress address with another client.

The endpoint is held for the `Retry-After` window before anything else goes to it. If you believe the table is
wrong for an endpoint, [say so](https://github.com/jerbersoft/ibkrdotnet/issues) — a measured limit is worth more
than an inferred one.

## `411 Length Required`

Should not happen, and is worth reporting if it does. Request bodies are serialized up front so they carry a
`Content-Length`; `JsonContent` cannot report its length, `HttpClient` falls back to chunked encoding, and the
edge in front of IBKR answers a chunked request with `411` before it ever reaches the API.

## `503` or `423` from an FYI endpoint

`/fyi/unreadnumber` and `/fyi/notifications` answer a cold call by saying they are not ready — `503`, or `423`
with `{"status":"waiting for reply"}` — while the gateway fetches from IBKR, then serve the data on a later call.
Neither status is documented on either endpoint.

The client does not retry for you, because every endpoint in that group allows one request per second and
spending your rate budget on a condition you cannot see is not a library's decision to make. Catch it on
`StatusCode` and decide.

## An alert read "succeeded" but everything is null

It did not succeed. An unknown alert comes back as `200 OK` carrying an `error` field. `GetDetailsAsync` checks
the body and throws, so if you are seeing nulls you are probably reading the response another way.

## A field the docs promise is missing

Common enough that the [reference](../../README.md#things-about-ibkr-that-will-otherwise-surprise-you) is mostly
this. Notable cases: the scanner's `scan_data` is absent from every row a live gateway returns; PortfolioAnalyst
hides its figures under a property named after the account; IBKR sends five event-contract fields it never
documented and eleven notification type codes that are on no list anywhere.

If you hit a new one, the exception carries the response body — that is what makes it fixable.
