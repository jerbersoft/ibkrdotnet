# Endpoint coverage

**90 of IBKR's 108 Trading endpoints** are implemented, across twelve endpoint clients. All but ten have been
exercised against a live gateway, executions included.

| Client | Group | Endpoints |
| --- | --- | --- |
| `ISessionClient` | Session | 5 |
| `IAccountsClient` | Accounts | 11 |
| `IPortfolioClient` | Portfolio | 13 |
| `IContractsClient` | Contracts and instrument discovery | 15 |
| `IOrdersClient` | Orders | 11 |
| `IMarketDataClient` | Market data | 4 |
| `IWatchlistsClient` | Watchlists | 4 |
| `IScannerClient` | Market scanner | 2 |
| `INotificationsClient` | FYIs and notifications | 11 |
| `IEventContractsClient` | Event contracts | 5 |
| `IAlertsClient` | Alerts | 5 |
| `IPortfolioAnalystClient` | PortfolioAnalyst | 4 |

All twelve hang off `IIbkrTradingClient`, and each is registered individually so a component can take just the one
it needs.

## The ten that unit tests cover and a live gateway has not

Seven are the **notification writes**. They change subscription and delivery settings on the username, and IBKR
documents no way to read a value before overwriting it, or to re-register a device once deleted. Running them
against a real account is not undoable, so the live sweep lists them as skips saying what each would have changed.

Three are the **alert reads that need an alert to already exist**. IBKR publishes five alert endpoints and no way
to create an alert — they have to be built in Trader Workstation or Client Portal. Which also makes
`DeleteAsync` one-way; to stop an alert firing without losing it, deactivate it.

## What is not implemented

Two Financial Advisor groups, tracked on the
[milestones](https://github.com/jerbersoft/ibkrdotnet/milestones):

**[FA Allocation Management](https://github.com/jerbersoft/ibkrdotnet/issues/21)** (8 endpoints) — splitting one
order across the sub-accounts an advisor manages, by NetLiq, equal weight or explicit percentages.

**[FA Model Portfolios](https://github.com/jerbersoft/ibkrdotnet/issues/22)** (10 endpoints) — named target
portfolios that client accounts subscribe to and are rebalanced toward.

Both need an FA master account with sub-accounts under it to verify against. An individual account has none, so
these would be the first group in the repository to ship on documentation fixtures alone, with no live-gateway
confirmation behind the models — which is where most of the corrections in the
[reference](../../README.md#things-about-ibkr-that-will-otherwise-surprise-you) came from.

**WebSocket streaming** is out of scope entirely and not planned.

## Reaching an endpoint that is not modelled

`IIbkrApiClient` is the escape hatch. It keeps authentication, rate limiting and error mapping; you supply the
path and the shape.

```csharp
public sealed class Advisor(IIbkrApiClient api)
{
    public Task<JsonElement> GetAllocationGroupsAsync(CancellationToken ct) =>
        api.SendAsync<JsonElement>(
            IbkrRequest.Get("/v1/api/iserver/account/allocation/group"), ct);
}
```

`IbkrRequest` builds the request — `Get`, `Post`, `Put`, `Delete`, with `WithQuery` and `WithJsonBody` — and
`SendAsync<T>` deserializes into any type you like, including your own record. `SendRawAsync` hands back the
`HttpResponseMessage` for a response that is not JSON.

Paths include the `/v1/api` prefix. An endpoint reached this way is paced by whichever limit in the table matches
its path, or by the global limit alone if none does.
