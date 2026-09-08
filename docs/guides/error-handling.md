# Error handling

Every failure is an exception. There is no `Try`-prefixed method and no method that reports a failure by
returning. A `null` or an empty list always means an answer IBKR genuinely gave.

## The hierarchy

```
IbkrApiException                       — the base. Any non-success response, and transport failures.
├── IbkrAuthenticationException        — 401 or 403
└── IbkrRateLimitExceededException     — 429, or a local limit that would be breached
```

Every one of them carries the context needed to act on it without re-deriving it:

```csharp
catch (IbkrApiException ex)
{
    ex.StatusCode;    // HttpStatusCode?, null for a transport failure
    ex.Method;        // "POST"
    ex.Path;          // "/v1/api/iserver/account/{id}/orders"
    ex.ResponseBody;  // truncated at 2 KB
}
```

`IbkrRateLimitExceededException` adds `RetryAfter` and `Limit`; see [Rate limits](rate-limits.md).

## A 401 is usually not about your credentials

```csharp
catch (IbkrAuthenticationException ex) when (ex.Path?.StartsWith("/v1/api/iserver") == true)
{
    // Almost always a lapsed brokerage session, not bad credentials.
    await ibkr.Session.EnsureBrokerageSessionAsync(ct);
}
```

On an `/iserver` endpoint this is the ordinary way a session expiry presents itself. See [Sessions](sessions.md)
for why a session can lapse even while a keep-alive is running.

The exception to that reading: `401 Invalid Consumer` under OAuth 1.0a, on a consumer key registered today. That
one is IBKR's midnight reset and no amount of re-authenticating will fix it before then —
[Authentication](authentication.md) has the detail.

## Three places where the transport lies

**An unknown alert is `200 OK`.** `GET /iserver/account/alert/{alertId}` answers a missing alert with a success
status carrying `{"error": "Alert with order ID=... not found."}`. Nothing about the transport says the read
failed and every field would deserialize to `null`, so `GetDetailsAsync` checks the body and throws.

**Two FYI endpoints answer a cold call by saying they are not ready.** `/fyi/unreadnumber` and
`/fyi/notifications` return `503 Service Unavailable`, or `423 Locked` with `{"status":"waiting for reply"}`,
while the gateway fetches from IBKR — then serve the data on a later call. Neither status is documented on either
endpoint.

The client does not retry for you. Every endpoint in that group is limited to one request per second, and
spending a caller's rate budget on a condition they cannot see is not a decision a library should make quietly.
`StatusCode` carries the status, so the policy is yours:

```csharp
catch (IbkrApiException ex) when (ex.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.Locked)
{
    // The gateway is still fetching. Ask again shortly.
}
```

**A forced order refresh answers with an empty array.** `GetOpenOrdersAsync(force: true)` clears IBKR's cache and
returns nothing; the orders arrive on the request after it. Not an error, and not an empty account.

## Empty is not always empty

Two silent-empty cases are worth guarding rather than diagnosing:

- A `/portfolio` endpoint returns empty or stale data for an account that has not been listed by
  `GetAccountsAsync` first. No error is reported.
- A brokerage session that is `Authenticated` but not yet `Established` answers account requests empty rather than
  failing.

Both are covered in [Sessions](sessions.md).

## A number that does not apply is an empty string

IBKR sends `""` where a value does not apply — a market order's `limit_price`, a filled order's `price` — and
`"None"` and `"N/A"` in the same role elsewhere. Every nullable numeric property absorbs these as `null`, so one
inapplicable field does not fail the whole response. A non-nullable one refuses rather than reading a plausible,
wrong zero, which is the behaviour you want the first time IBKR sends something new.

## Deserialization failures

A body that cannot be read as the expected type throws `IbkrApiException` with the JSON error and the truncated
body attached. That is the failure mode to report as a bug here: it usually means IBKR changed a shape, and the
body in the exception is what makes it fixable.

## Cancellation

Every method takes a `CancellationToken` and throws `OperationCanceledException` when it fires. A timeout is
different — that surfaces as `IbkrApiException` saying the request timed out, so a cancelled request and a slow
one are never confused.
