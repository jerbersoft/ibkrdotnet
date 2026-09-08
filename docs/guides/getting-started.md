# Getting started

This walks from nothing to a working call against a paper account. It assumes the Client Portal Gateway, because
that is the only one of IBKR's three authentication mechanisms you can set up by yourself in an afternoon — see
[Authentication](authentication.md) for the other two and why they take weeks.

## What you need first

An **IBKR Pro account with funds in it**. IBKR Lite has no API access, and an unfunded account will authenticate
and then answer most endpoints with nothing.

For paper trading you need a **separate paper username**, not your live one. The gateway's login page has no
live/paper switch; which account you reach is decided by which username you type.

## Run the gateway

The gateway is a small Java program that performs the browser login and proxies authenticated requests. Java has
to be on the machine — `java -version` should answer.

Download and unzip it from IBKR's
[installation guide](https://ibkrcampus.com/docs/web-api/authentication/cpgw/installation-authentication), then
from the unzipped `clientportal.gw` directory:

```sh
bin/run.sh root/conf.yaml          # bin\run.bat root\conf.yaml on Windows
```

Open <https://localhost:5000> and log in. The certificate is self-signed, so the browser will object; that is
expected for a program serving on loopback. Once the page says you are authenticated, leave the gateway running —
it holds the session, and this library talks to it rather than to IBKR.

> **Port 5000 is taken on macOS** by the AirPlay receiver. Change it in `root/conf.yaml` — `listenPort` on line 4,
> conventionally to 5001 — and point the client at the new address with `options.BaseAddress`. It is a file
> setting, not a command-line flag.

## Install

```sh
dotnet add package IbkrDotNet.Extensions.DependencyInjection
```

That package is the registration surface and brings `IbkrDotNet.Trading` with it. A consumer with a container of
its own can reference `IbkrDotNet.Trading` alone and construct the clients directly.

> **Not on nuget.org yet.** Until it is, `dotnet pack` into a local feed, or reference the projects.

## Register the client

```csharp
builder.Services
    .AddIbkrTrading(options =>
    {
        options.Environment = IbkrEnvironment.ClientPortalGateway;
        options.UserAgent   = "contoso-trader/1.0";   // IBKR asks every client to identify itself
    })
    .UseClientPortalGateway()
    .AddBrokerageSessionKeepAlive();
```

`AddBrokerageSessionKeepAlive` registers a hosted service that establishes the brokerage session at startup and
pings `/tickle` every 60 seconds. Without it the session goes idle after about five minutes and you have to keep
it alive yourself. [Sessions](sessions.md) explains what it is keeping alive and why that is only half the
problem.

## Make a call

```csharp
public sealed class Positions(IIbkrTradingClient ibkr)
{
    public async Task ReportAsync(CancellationToken ct)
    {
        await ibkr.Session.EnsureBrokerageSessionAsync(ct);

        // Call this before any other /portfolio endpoint. Others return empty or stale data for an
        // account that has not been listed, and IBKR reports no error when they do.
        var accounts = await ibkr.Portfolio.GetAccountsAsync(ct);
        var account  = accounts[0].AccountId;

        var summary   = await ibkr.Portfolio.GetSummaryAsync(account, ct);
        var positions = await ibkr.Portfolio.GetPositionsAsync(account, cancellationToken: ct);

        foreach (var position in positions)
        {
            Console.WriteLine($"{position.ContractDescription} {position.Quantity} @ {position.MarketPrice}");
        }
    }
}
```

`IIbkrTradingClient` groups all twelve endpoint clients. Inject one — `IPortfolioClient`, `IOrdersClient` — where
a component only needs one; they are registered individually as well.

## Try it without writing anything

The repository ships a read-only console tour: session status, accounts, balances, a quote, daily bars and an
order *preview*. It never places a live order.

```sh
dotnet run --project samples/IbkrDotNet.Samples.Console
```

Against a gateway on another port, trusting its self-signed certificate:

```sh
dotnet run --project samples/IbkrDotNet.Samples.Console -- \
    --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
```

`--TrustGatewayCertificate` relaxes certificate validation for loopback addresses only, so it cannot quietly
disable it against a real IBKR host.

## Where to go next

- [Authentication](authentication.md) — the three mechanisms, and which one you are eligible for
- [Sessions](sessions.md) — what a brokerage session is, and why a keep-alive is not enough
- [Placing orders](orders.md) — the reply workflow, which is where most first attempts stall
- [Market data](market-data.md) — why your first snapshot comes back empty
- [Troubleshooting](troubleshooting.md) — the failures that look like bugs in your code and are not
