# IbkrDotNet

A .NET client for the [Interactive Brokers Web API](https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-trading/) **Trading** surface.

| Package | Description |
| --- | --- |
| `IbkrDotNet.Trading` | Endpoint clients, models, authentication and transport. |
| `IbkrDotNet.Extensions.DependencyInjection` | `AddIbkrTrading(...)` for `Microsoft.Extensions.DependencyInjection`. |

Targets `net10.0`. Every date and time value in the public API is a [NodaTime](https://nodatime.org) type — there is no `DateTime`, `DateTimeOffset` or `TimeSpan` anywhere in it.

> **Status: in development.** The core trading path (session, accounts, portfolio, contracts, orders, market data) plus watchlists, the market scanner and FYIs & notifications — 76 of IBKR's 108 Trading endpoints — is implemented. All but seven have been exercised against a live gateway, executions included; the seven are the notification writes, which change settings on the username and cannot be undone through the API. The rest is tracked in the [milestones](https://github.com/jerbersoft/ibkrdotnet/milestones).

## Getting started

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
public sealed class Trader(IIbkrTradingClient ibkr)
{
    public async Task<OrderSubmissionResult> BuyAsync(AccountId account, ConId conId, CancellationToken ct)
    {
        await ibkr.Session.EnsureBrokerageSessionAsync(ct);

        return await ibkr.Orders.SubmitAsync(account, [new OrderTicket
        {
            ConId        = conId.Value,
            OrderType    = "LMT",
            Side         = OrderSide.Buy,
            TimeInForce  = TimeInForce.Day,
            Quantity     = 100,
            Price        = 165.00m,
        }], cancellationToken: ct);
    }
}
```

Inject an individual client — `IOrdersClient`, `IPortfolioClient`, and so on — where a component only needs one.

## Choosing an authentication mechanism

IBKR offers three ways in. They differ only in how a request is credentialed; resource paths and payloads are identical, so switching is a configuration change.

| Mechanism | For | Setup |
| --- | --- | --- |
| **Client Portal Gateway** | Retail and individual clients | `.UseClientPortalGateway()` — the default |
| **OAuth 2.0** | Organizations, Financial Advisors, IBrokers | `.UseOAuth2(o => ...)` |
| **OAuth 1.0a** | Financial Advisors, organizations, third-party vendors | `.UseOAuth1a(o => ...)` |

### Client Portal Gateway

Download and run IBKR's gateway, then log in at `https://localhost:5000`. The gateway holds the credentials and proxies authenticated requests, so nothing needs signing on this side. Its certificate is self-signed, so the first request fails on TLS until you trust it.

If the gateway is on another port — macOS serves its AirPlay receiver on 5000, so moving it is common — set `BaseAddress` rather than `Environment`:

```csharp
options.BaseAddress = new Uri("https://localhost:5050");
```

### OAuth 2.0

```csharp
.UseOAuth2(o =>
{
    o.ClientId        = "...";     // issued at registration
    o.ClientKeyId     = "...";     // identifies the registered public key
    o.Credential      = "...";     // the IBKR username the session is for
    o.ClientIpAddress = "...";     // IBKR validates this against the request's origin
    o.UsePrivateKeyFile("/secrets/ibkr-oauth2.pem");
})
```

`ClientIpAddress` has no default. IBKR validates the claim against the address the request actually arrives from, and its reference implementation discovers it by calling a third-party lookup service — not something this library will do on your behalf unasked. Set it, or supply `ClientIpAddressResolver`.

### OAuth 1.0a

```csharp
.UseOAuth1a(o =>
{
    o.ConsumerKey        = "...";
    o.Realm              = OAuth1aOptions.LimitedPoaRealm;  // TestRealm for TESTCONS
    o.AccessToken        = "...";
    o.AccessTokenSecret  = "...";   // base64, still encrypted
    o.DiffieHellmanPrime = "...";   // issued with the consumer key
    o.UseEncryptionKeyFile("/secrets/ibkr-encryption.pem"); // decrypts the token secret
    o.UseSignatureKeyFile("/secrets/ibkr-signature.pem");   // signs the handshake
})
```

The encryption and signing keys are different keys. Swapping them produces a live session token that fails IBKR's signature check, which the client reports rather than using.

## Things about IBKR that will otherwise surprise you

**Sessions are two-tiered.** An outer read-only session gates every request but only reaches non-`/iserver` endpoints. A separate *brokerage* session gates trading, market data and everything else behind `/iserver`. A username may hold only one brokerage session at a time across all platforms, so logging into Trader Workstation displaces one held here. `EnsureBrokerageSessionAsync` establishes it; `BrokerageSessionStatus` keeps IBKR's four flags separate, and `Established` — not `Authenticated` — is the one to gate trading on.

**Sessions time out.** After a few idle minutes IBKR drops the session. `AddBrokerageSessionKeepAlive()` pings `/tickle` every 60 seconds; without it, call `IIbkrSessionManager.KeepAliveAsync` yourself.

**The first market data snapshot returns nothing.** IBKR treats it as a pre-flight that starts the backend streaming the instrument; snapshots are read from those open streams, not from a cache. Send the pre-flight with every field you will later want, then ask again. Each subscribed instrument consumes one of your market data lines (100 by default), so unsubscribe when you are done.

**Call `/portfolio/accounts` first.** Other `/portfolio` endpoints return empty or stale data for an account until it has been listed. IBKR does not report an error, which makes this a slow thing to diagnose.

**An order reply message is not a rejection.** Submission can answer with a prompt IBKR wants confirmed — usually a precautionary limit configured on your username. `SubmitAsync` returns `OrderSubmissionResult.ReplyRequired`, and the default `OrderReplyPolicy.Manual` leaves the decision to you, because these prompts carry margin, liquidity and price-constraint warnings. `OrderReplyPolicy.AutoConfirm` is opt-in. To stop being asked at all, suppress the message categories at the start of the session with `SuppressMessagesAsync`.

**Rate limits are enforced client-side by default.** IBKR caps requests at 10/second per username and applies much tighter per-endpoint limits — `/iserver/scanner/params` allows one request per fifteen minutes. Exceeding them puts your IP in a ten-minute penalty box, and repeat violations can get it blocked, so the client paces requests rather than reacting to a `429`. Waits longer than `RateLimiting.MaxWait` (30 seconds) throw instead of blocking silently.

**A market scanner selects contracts; it does not report the numbers it ranked them by.** Each row carries a `scan_data` field holding the ranked value, and IBKR's published example shows it on every row — but a live gateway omitted it from all fifty rows of a Top % Gainers scan, both pre-market and during regular trading hours, returning only the contracts and the column heading. Read quotes for the returned conids if the numbers matter. `/iserver/scanner/params` is separately awkward: it is 200 KB of reference data behind the tightest limit in the API, one request per fifteen minutes, so fetch it once and hold it. A `combo` filter's choices come back carrying nothing but which one is the default — no value, no label — so what to send for one has to be read out of Trader Workstation.

**A number that does not apply comes back as an empty string.** A market order's `limit_price`, a filled order's `price` — IBKR sends `""` rather than `null` or nothing at all, and `"None"` and `"N/A"` appear in the same role elsewhere. Every nullable numeric property absorbs these as `null`, so one inapplicable field does not fail the whole response. A non-nullable one refuses instead of reading a plausible, wrong zero.

**IBKR's list of notification type codes is not the list it sends.** The reference publishes twenty-three `typecode` values as a closed enum. A live gateway returns thirty categories from `/fyi/settings`, eleven of which — `OI`, `AA`, `BR`, `EH`, `NS`, `NP`, `PF`, `PC`, `SP`, `SL`, `TP` — are on no list anywhere, while four that are documented never appear. IBKR's *own* example response for that endpoint contains `PF`. So `NotificationTypeCode` is an open struct with the documented codes as static members, not an enum: an enum would turn every unlisted code into a failure of the whole response. The same group documents a notification's read flag as a string and sends a number, documents an `HT` field it never sends, and sends an `SS` field it never documented.

**Two FYI endpoints answer a cold call by saying they are not ready.** `/fyi/unreadnumber` and `/fyi/notifications` return `503 Service Unavailable`, or `423 Locked` with the body `{"status":"waiting for reply"}`, while the gateway fetches from IBKR — then serve the data on a later call. Neither status is documented on either endpoint. The client does not retry for you: every endpoint in the group is limited to one request per second, and spending a caller's rate budget on a condition they cannot see is not a decision a library should make quietly. `IbkrApiException.StatusCode` carries the status, so the policy is yours.

**Time is encoded inconsistently, which is why this library uses NodaTime.** The same API sends epoch seconds, epoch milliseconds, epoch milliseconds inside a JSON string, `YYYYMMDD-hh:mm:ss`, `YYMMDDhhmmss`, `yyyyMMdd` and `HHmm` — sometimes two encodings of one value on the same object. Each field declares the converter for its documented format, so `ledger.RetrievedAt` (seconds) and `trade.TradeTime` (milliseconds) both arrive as a correct `Instant`. Where IBKR sends the same moment twice — `trade_time` beside `trade_time_r`, `lastExecutionTime` beside `lastExecutionTime_r` — a real fill on a live gateway confirms the text halves are UTC and that both decodings land on the same instant. Trading schedules go further: opening and closing times are `LocalTime` values in the venue's own zone, reported as an IANA identifier, and `tradingScheduleDate` can mean "any Saturday" rather than a date — see `TradingScheduleDate`.

## Endpoints not yet modelled

`IIbkrApiClient` reaches anything this library has not covered yet, keeping authentication, rate limiting and error mapping:

```csharp
var watchlists = await apiClient.SendAsync<JsonElement>(
    IbkrRequest.Get("/v1/api/iserver/watchlists"), ct);
```

## Working on this repository

```bash
dotnet build IbkrDotNet.slnx -c Release
dotnet test -c Release
```

Response fixtures ending `.live.json` were captured from a running gateway rather than lifted from the documentation. They exist where the two disagree, so the discrepancy is pinned by a test instead of rediscovered.

`tools/fetch-spec.sh` downloads IBKR's reference documentation as Markdown into a gitignored `artifacts/spec/`. IBKR serves a clean Markdown rendering of any docs page by appending `.md` to its URL, which makes it a reliable source when adding or verifying endpoint models. The response fixtures under `tests/IbkrDotNet.Trading.Tests/Fixtures/Responses/` are the example payloads from those pages, so deserialization is checked against what the API actually emits.

`samples/IbkrDotNet.Samples.Console` is a read-only tour against a locally running gateway: session status, accounts, balances, a quote, daily bars and an order *preview*. It never places a live order.

```bash
dotnet run --project samples/IbkrDotNet.Samples.Console
```

Point it at a gateway on another port, and trust that gateway's self-signed certificate, with:

```bash
dotnet run --project samples/IbkrDotNet.Samples.Console -- \
    --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
```

`--TrustGatewayCertificate` relaxes certificate validation for loopback addresses only, so it cannot quietly disable it for a real IBKR host.

`samples/IbkrDotNet.Samples.Verify` sweeps every implemented endpoint against a running gateway and prints one line per endpoint. It exists because the unit tests cannot see the two kinds of bug that matter most here: a transport problem that only an intermediary produces, and a response whose real shape contradicts the documented example the fixtures were built from.

```bash
dotnet run --project samples/IbkrDotNet.Samples.Verify -- \
    --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
```

No order is submitted by default. `--Orders=true` adds the order write path, which submits a limit order priced a quarter below the market so it rests rather than fills, modifies it, and cancels it in a `finally` block.

`--Fills=true` goes further and lets an order execute: it buys one share at market, reads the execution back, and sells it again in a `finally` block, checking that the position returns to whatever the account started with rather than assuming it started flat. This is the only way to reach the four execution-time encodings, and it is worth reaching — the first run of it found that a market order's `limit_price` arrives as `""`, which failed the whole response. Unlike the rest of the sweep, these checks assert on the data: IBKR sends each execution time twice, and the two decodings disagreeing is a converter bug rather than a fact about the account.

Both flags are refused on anything but a paper account, and there is deliberately no flag to override that.

The notification checks read and never write, and there is no flag to make them write. Every other write path in the sweep undoes itself, but these change subscriptions and delivery settings on the username, and IBKR documents no way to read a value before overwriting it or to re-register a device once deleted. The seven writes are listed in the report as skips, each saying what it would have changed, so the group is visible in full rather than half-absent.

The watchlist checks do write without a flag, because a watchlist cannot move money. One is created under a fixed identifier, read back to confirm its contents, and deleted in a `finally` block; the identifier is checked against the existing lists first, so a watchlist the user created is never displaced.

No credential is read or printed: the gateway holds the login, and the account identifier is discovered at runtime and masked on the way out, so the output can go straight into a bug report.

## License

MIT. See [LICENSE](LICENSE).
