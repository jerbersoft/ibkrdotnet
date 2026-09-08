---
_layout: landing
---

<div class="text-center my-5">
  <h1 class="display-4 fw-bold">IbkrDotNet</h1>
  <p class="lead">
    A .NET 10 client for the
    <a href="https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-trading/">Interactive Brokers Web API</a>
    <strong>Trading</strong> surface — all three authentication mechanisms, NodaTime throughout, and IBKR's
    measured behaviour documented on the members it affects.
  </p>
  <p>
    <a class="btn btn-primary btn-lg" href="guides/getting-started.md">Get started</a>
    <a class="btn btn-outline-secondary btn-lg" href="../README.md">Reference</a>
    <a class="btn btn-outline-secondary btn-lg" href="api/index.md">API reference</a>
  </p>
</div>

```csharp
builder.Services
    .AddIbkrTrading(options =>
    {
        options.Environment = IbkrEnvironment.ClientPortalGateway;
        options.UserAgent   = "contoso-trader/1.0";
    })
    .UseClientPortalGateway()
    .AddBrokerageSessionKeepAlive();
```

```csharp
await ibkr.Session.EnsureBrokerageSessionAsync(ct);

var accounts  = await ibkr.Portfolio.GetAccountsAsync(ct);
var summary   = await ibkr.Portfolio.GetSummaryAsync(accounts[0].AccountId, ct);
var quote     = await ibkr.MarketData.GetSnapshotAsync([conId], [MarketDataField.LastPrice], ct);
```

Twelve endpoint groups hang off `IIbkrTradingClient`, or inject one — `IOrdersClient`, `IPortfolioClient` — where
a component only needs one. Ninety of IBKR's 108 Trading endpoints are implemented, all but ten of them exercised
against a live gateway.

## Install

Two packages. `IbkrDotNet.Extensions.DependencyInjection` is the registration surface and brings
`IbkrDotNet.Trading`, the client, with it. A consumer with a container of its own can reference the client alone.

```sh
dotnet add package IbkrDotNet.Extensions.DependencyInjection
```

> **Not on nuget.org yet.** Both packages build and pack from this repository; publishing is deliberately
> deferred. Until then, `dotnet pack` and a local feed, or a project reference.

## The four things worth knowing up front

**Getting in is the hard part, and it is not a code problem.** Of IBKR's three authentication mechanisms only the
Client Portal Gateway is self-serve. Both OAuth paths start with an email to IBKR and an approval measured in
weeks, and OAuth 2.0 is closed to individual account structures outright. See
[Authentication](guides/authentication.md).

**A brokerage session is not a login.** An outer session gates every request; a separate brokerage session gates
everything behind `/iserver`, which is trading, market data and most of what you came for. One username holds one
brokerage session across all platforms, so opening Trader Workstation takes yours. See
[Sessions](guides/sessions.md).

**Rate limits are enforced here, not discovered there.** Exceeding IBKR's limits puts your IP in a ten-minute
penalty box; repeat violations get it blocked. The client paces requests against the published limits and feeds a
`429` back into that pacing. See [Rate limits](guides/rate-limits.md).

**Time is NodaTime, all the way through.** No `DateTime`, `DateTimeOffset` or `TimeSpan` appears in any public
signature. IBKR encodes time ten different ways — sometimes two encodings of one value on the same object — which
is exactly the class of bug NodaTime exists to make unrepresentable. See
[Dates and times](guides/dates-and-times.md).

## Status

In development. The core trading path plus watchlists, the scanner, FYIs and notifications, event contracts,
alerts and PortfolioAnalyst are implemented; the two Financial Advisor groups are not. See
[Endpoint coverage](guides/endpoint-coverage.md) for what is in and what is not, and the
[milestones](https://github.com/jerbersoft/ibkrdotnet/milestones) for what is next.

This is an independent client. It is not affiliated with, endorsed by, or supported by Interactive Brokers.
