# Sessions

A brokerage session is not a login, and this is the single most common thing to get wrong.

## Two tiers

An **outer session** — the browser login at the gateway, or the SSO session an OAuth handshake produces — gates
every request. It reaches `/portfolio`, `/trsrv`, `/fyi` and the rest.

A separate **brokerage session** gates everything behind `/iserver`: trading, market data, order history, the
scanner. Establishing the outer one does not establish this one.

```csharp
await ibkr.Session.EnsureBrokerageSessionAsync(ct);
```

`IIbkrSessionManager` is what you want in application code. `ISessionClient` is the raw endpoint group underneath
it — `GetStatusAsync`, `TickleAsync`, `InitializeAsync`, `ValidateAsync`, `LogoutAsync` — and is there for a
caller who wants to drive the individual calls.

## Four flags, and only one of them means ready

`BrokerageSessionStatus` keeps IBKR's flags separate rather than collapsing them into "logged in":

| Flag | Means |
| --- | --- |
| `Connected` | There is a physical connection to IBKR's brokerage infrastructure |
| `Authenticated` | Initial authentication passed. The session may not be initialized yet |
| `Established` | Fully initialized, account information loaded, ready for requests |
| `Competing` | Another session is holding the same username |

**Gate trading on `Established`, not `Authenticated`.** A session can be connected and authenticated while
account information has not loaded, and in that state requests for it come back *empty rather than failing* —
which is the worst way for this to go wrong, because nothing in the response says anything is amiss.

## One session per username, across everything

An IBKR username holds one brokerage session at a time across all platforms. Logging into Trader Workstation or
Client Portal with the same username displaces the one your application is holding, and `Competing` is how you
find out. There is no way to hold two; the fix is a second username.

## Timing out and expiring are different problems

**Idle timeout, ~5 minutes.** IBKR drops an idle session. `AddBrokerageSessionKeepAlive()` pings `/tickle` every
60 seconds to stop that; without it, call `IIbkrSessionManager.KeepAliveAsync` yourself on a timer.

**Outright expiry, 24 hours.** A session expires at midnight in New York, Zug or Hong Kong — whichever region you
connect nearest to — no matter how diligently it was tickled. A keep-alive does not prevent this and cannot.

So a long-running process has to be able to *establish a new session*, not merely maintain one. If your service
runs overnight, it will meet this. `EnsureBrokerageSessionAsync` is safe to call again: it checks status first and
only initializes when it has to, which makes "call it before a batch of work" a reasonable pattern.

```csharp
public async Task<IReadOnlyList<Position>> ReadAsync(AccountId account, CancellationToken ct)
{
    // Cheap when the session is already up; re-establishes it when the midnight reset has been through.
    var status = await ibkr.Session.EnsureBrokerageSessionAsync(ct);
    if (!status.Established)
    {
        throw new InvalidOperationException($"Brokerage session not established: {status.Message ?? status.Fail}");
    }

    await ibkr.Portfolio.GetAccountsAsync(ct);          // see below
    return await ibkr.Portfolio.GetPositionsAsync(account, cancellationToken: ct);
}
```

## Call `/portfolio/accounts` first

Not a session rule exactly, but it fails the same silent way. Other `/portfolio` endpoints return empty or stale
data for an account until it has been listed by `GetAccountsAsync`. IBKR reports no error, so this is a slow thing
to diagnose from the outside.

## The keep-alive service

```csharp
.AddBrokerageSessionKeepAlive(o =>
{
    o.Interval                = Duration.FromSeconds(60);
    o.EstablishSessionOnStart = true;
})
```

A `BackgroundService`. Failures are logged and retried on the next tick rather than stopping the host — a
transient network problem or IBKR's nightly `/iserver` maintenance window should not take an application down.

`/tickle` is itself limited to one request per second, and the idle timeout is minutes, so 60 seconds sits
comfortably between the two. There is little value in going faster and some risk in it.

## Logging out

`LogoutAsync` ends the session. Under the gateway that leaves the gateway running but unauthenticated, so the next
call fails until somebody logs in through the browser again — worth knowing before wiring it into a shutdown hook.
