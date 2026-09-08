# Rate limits

IBKR's response to a breach does not land on the request. It lands on your IP address: a ten-minute penalty box,
and repeat violations can get it blocked until the issue is resolved. So this client paces requests locally rather
than reacting to a `429`, and pacing is on by default.

## What is enforced

A **global cap of 10 requests per second** per authenticated username, plus per-endpoint limits that are much
tighter. A sample:

| Endpoint | Limit |
| --- | --- |
| `/iserver/scanner/params` | 1 per **15 minutes** |
| `/pa/*` (PortfolioAnalyst) | 1 per **15 minutes** each |
| `/iserver/account/trades`, `/iserver/account/orders`, `/portfolio/accounts` | 1 per 5 seconds |
| `/sso/validate` | 1 per minute |
| `/tickle`, `/fyi/*` | 1 per second |
| `/iserver/marketdata/snapshot` | 10 per second |

The full table is `IbkrRateLimits.PerEndpoint`, matched first-match-wins with `{name}` segments matching any
single path segment — so `/fyi/notifications/{id}` is one bucket regardless of which notification.

**Parts of that table are inference.** `/pa/allperiods` appears in no IBKR limits table at all and is paced with
its neighbours; `/pa/summary` is in the table but is not an endpoint IBKR documents anywhere. Being wrong in the
permissive direction costs an IP a penalty box, so the inference errs tight.

## Where the waiting happens

Pacing runs in `IbkrApiClient`, *before* the request reaches `HttpClient` — not in a `DelegatingHandler`.

`HttpClient.Timeout` covers the whole handler chain, so a wait taken inside it is charged to the caller's request
budget. With the shipped defaults the longest permitted wait and the whole request budget are both thirty seconds,
and a paced request would fail as "timed out" without a byte having left the process. Waiting outside costs
nothing, and the timeout goes back to measuring only what IBKR is responsible for.

`IbkrRateLimitHandler` exists for a caller assembling their own pipeline, and its documentation says the same
thing: use it only on a client whose timeout is infinite or comfortably larger than the longest wait a limit can
impose.

## The limiters are a singleton

`IbkrRateLimiterRegistry` is registered as a singleton because `IHttpClientFactory` rotates handler chains on its
own lifetime — two minutes by default. Building the windows inside a handler would reset every one of them on
each rotation: invisible against a one-second limit, and completely wrong against a fifteen-minute one.

## A wait too long is reported, not taken

```csharp
try
{
    var parameters = await ibkr.Scanner.GetParametersAsync(ct);
}
catch (IbkrRateLimitExceededException ex)
{
    // ex.Limit     — which limit, e.g. "GET /v1/api/iserver/scanner/params"
    // ex.RetryAfter — how long until a permit is available
}
```

Blocking silently for the fifteen minutes some endpoints require would be indistinguishable from a hang, so past
`RateLimiting.MaxWait` (30 seconds) the client throws instead. Raise it if you would rather wait:

```csharp
options.RateLimiting.MaxWait = Duration.FromMinutes(20);
```

## A `429` is fed back into the pacing

If a rejection arrives anyway, the client holds that endpoint before sending to it again, rather than only
reporting the failure and pacing the next call by the same figure that was just rejected.

| What IBKR sent | Hold applied |
| --- | --- |
| `Retry-After`, as a delay or an absolute date | the window it named |
| nothing, on a path a published limit covers | that limit's own window — the figure already believed |
| nothing, on a path nothing covers | `RateLimiting.DefaultRetryAfter`, 5 seconds |

IBKR does not document sending `Retry-After` at all, which is why the fallbacks matter more than the header.

Two things the hold deliberately does not do:

**It does not rewrite the limits table.** A `429` can equally come from a second process on your username, an
address you share, or something transient at IBKR's end. The hold expires and pacing returns to normal rather than
permanently halving a healthy client's throughput on one bad response.

**It does not stall everything.** The hold covers the limit that paced the request, or the exact path when nothing
did. One tight endpoint should not become an outage for every other call in the process. The cost is that an
address genuinely in IBKR's penalty box surfaces as a rejection per path rather than a single stall.

## Turning it off

```csharp
options.RateLimiting.Enabled = false;
```

Rarely a good idea, and it disables the `429` feedback with it. The exception still reports what IBKR asked for so
you can honour it yourself. `EnforceGlobalLimit` disables just the 10/second cap, leaving the per-endpoint limits
in place.

## What paces and what does not

Only requests made through `IbkrApiClient` — which is every endpoint client, and `SendAsync`/`SendRawAsync` for
endpoints this library has not modelled. The OAuth token exchanges run on their own `HttpClient` outside the
pipeline, because routing them through the authenticating handler would recurse.
