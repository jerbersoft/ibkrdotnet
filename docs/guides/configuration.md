# Configuration

Everything the client does is configured through `IbkrTradingOptions`, bound to the `Ibkr` section by convention
(`IbkrTradingOptions.SectionName`).

## In code

```csharp
services.AddIbkrTrading(options =>
{
    options.Environment = IbkrEnvironment.ClientPortalGateway;
    options.UserAgent   = "contoso-trader/1.0";
    options.Timeout     = Duration.FromSeconds(30);
})
.UseClientPortalGateway();
```

## From configuration

```csharp
services.AddIbkrTrading(configuration.GetSection("Ibkr"))
        .UseClientPortalGateway();
```

```json
{
  "Ibkr": {
    "Environment": "ClientPortalGateway",
    "BaseAddress": "https://localhost:5001",
    "UserAgent": "contoso-trader/1.0",
    "Timeout": "00:00:30",
    "RateLimiting": {
      "Enabled": true,
      "EnforceGlobalLimit": true,
      "MaxWait": "00:00:30",
      "DefaultRetryAfter": "00:00:05"
    }
  }
}
```

Every key is optional. Durations accept a `TimeSpan` string, NodaTime's round-trip form, or a whole number of
seconds — they are bound explicitly rather than by reflection, because `Duration` is not a type the configuration
binder can construct.

## The settings

**`Environment`** picks the host: `ClientPortalGateway` (`https://localhost:5000`), `Production`
(`https://api.ibkr.com`) or `Sandbox` (`https://qa.interactivebrokers.com`). Both OAuth mechanisms talk to
`Production` directly; only the gateway path uses an intermediary.

**`BaseAddress`** overrides `Environment` outright. This is what you set when the gateway has been moved off port
5000 — which on macOS it has to be, because the AirPlay receiver holds that port.

```csharp
options.BaseAddress = new Uri("https://localhost:5050");
```

The address is the bare host. Endpoint paths in this library include the `/v1/api` prefix themselves.

**`UserAgent`** is sent with every request and IBKR asks that every client set one. It defaults to
`IbkrDotNet.Trading/<version>`, and registration fails at startup if it is blank.

**`Timeout`** is the per-request budget, 30 seconds by default. It measures what IBKR is responsible for and
nothing else — rate-limit pacing happens before the request reaches `HttpClient`, so a paced request does not
spend its timeout waiting. [Rate limits](rate-limits.md) explains why that arrangement matters.

**`RateLimiting`** is covered in full on [its own page](rate-limits.md). The short version: leave it on.

## Validation happens at startup

`AddIbkrTrading` calls `ValidateOnStart`, so a blank `UserAgent`, a non-positive `Timeout` or a negative
`MaxWait` fails when the host starts rather than at the first request — when a misconfiguration is far more
expensive to diagnose.

## Substituting the clock and the time zone provider

NodaTime's `IClock` and `IDateTimeZoneProvider` are registered with `TryAdd`, so a host can replace them:

```csharp
services.AddSingleton<IClock>(new FakeClock(Instant.FromUtc(2026, 1, 1, 0, 0)));
services.AddIbkrTrading(...);
```

Everything time-dependent reads the clock rather than `SystemClock.Instance` directly — token expiry, keep-alive
scheduling, OAuth nonces, rate-limit windows — so a test can move time without waiting for it.

## The keep-alive

```csharp
.AddBrokerageSessionKeepAlive(o =>
{
    o.Interval                = Duration.FromSeconds(60);
    o.EstablishSessionOnStart = true;
})
```

`Interval` sits between IBKR's idle timeout of roughly five minutes and `/tickle`'s own limit of one request per
second. Failures are logged and retried on the next tick rather than stopping the host: a transient network
problem, or IBKR's nightly `/iserver` maintenance window, should not take an application down with it.

## Registering more than one client

The registration is not keyed, so one service collection holds one IBKR client. An application that trades two
accounts under two usernames wants two hosts, or its own container scope — the brokerage session is per username,
and IBKR allows one at a time.
