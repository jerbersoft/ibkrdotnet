# IbkrDotNet

A .NET client for the [Interactive Brokers Web API](https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-trading/) **Trading** surface.

| Package | Description |
| --- | --- |
| `IbkrDotNet.Trading` | Endpoint clients, models, authentication and transport. |
| `IbkrDotNet.Extensions.DependencyInjection` | `AddIbkrTrading(...)` for `Microsoft.Extensions.DependencyInjection`. |

Targets `net10.0`. Every date and time value in the public API is a [NodaTime](https://nodatime.org) type — there is no `DateTime`, `DateTimeOffset` or `TimeSpan` anywhere in it.

**Documentation:** guides and the generated API reference are built from `docs/` — see [Working on this repository](#working-on-this-repository) for how to build and serve them locally. This file is the reference the guides link back to.

> **Status: in development.** The core trading path (session, accounts, portfolio, contracts, orders, market data) plus watchlists, the market scanner, FYIs & notifications, event contracts, alerts and PortfolioAnalyst — 90 of IBKR's 108 Trading endpoints — is implemented. All but ten have been exercised against a live gateway, executions included. The ten are the seven notification writes, which change settings on the username and cannot be undone through the API, and the three alert endpoints that need an alert to already exist — IBKR publishes no endpoint that creates one. The rest is tracked in the [milestones](https://github.com/jerbersoft/ibkrdotnet/milestones).

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

IBKR offers three ways in. They differ only in how a request is credentialed — resource paths and payloads are identical, so switching is a configuration change.

| Mechanism | Who can use it | How you get in |
| --- | --- | --- |
| **Client Portal Gateway** | Anyone with an IBKR Pro account | Self-serve: download and run IBKR's gateway |
| **OAuth 2.0** | Organizations, Financial Advisors, IBrokers. **Not individuals** | Apply by email, then register a public key |
| **OAuth 1.0a** | Financial Advisors, organizations, third-party vendors | Apply by email, then a self-service portal |

Only the first is self-serve. Both OAuth paths open with an email to IBKR and an approval process measured in weeks, so if you are an individual trading your own account, the gateway is the path — OAuth 2.0 is closed to individual account structures outright.

The two OAuth mechanisms talk to `https://api.ibkr.com` directly and need no gateway process running; set `Environment = IbkrEnvironment.Production` with either.

### Client Portal Gateway

A Java process that holds your credentials and proxies authenticated requests, so nothing needs signing on this side.

1. Have a funded IBKR Pro account.
2. Install Java, if `java -version` does not answer.
3. Download the gateway from IBKR's [installation guide](https://ibkrcampus.com/docs/web-api/authentication/cpgw/installation-authentication) and unzip it.
4. From the unzipped `clientportal.gw` directory: `bin/run.sh root/conf.yaml` — on Windows, `bin\run.bat root\conf.yaml`.
5. Log in at `https://localhost:5000` with your IBKR credentials.

```csharp
builder.Services
    .AddIbkrTrading(options => options.Environment = IbkrEnvironment.ClientPortalGateway)
    .UseClientPortalGateway();
```

The gateway's certificate is self-signed, so the first request fails on TLS until you trust it.

**A paper account needs a different username.** The gateway login has no live/paper slider — the paper account has credentials of its own. Find them in Client Portal under Settings → Account Configuration → [Paper Trading Account](https://ibkrcampus.com/docs/web-api/authentication/paper), where you can also reset the paper password if you have never set one.

**To move off port 5000** — macOS serves its AirPlay receiver there, so this comes up often — edit `listenPort` on line 4 of `root/conf.yaml` inside the gateway directory and restart. There is no command-line flag for it. Then set `BaseAddress` rather than `Environment`:

```csharp
options.BaseAddress = new Uri("https://localhost:5050");
```

### OAuth 2.0

**Getting credentials.** Email [api-solutions@interactivebrokers.com](mailto:api-solutions@interactivebrokers.com) for registration guidance; the scopes you are granted are decided during that process. Once approved, generate an RSA keypair of at least 3072 bits and send IBKR **the public key only**, through the Secure Message Center, from a user on the live account:

```bash
openssl genrsa -out privatekey.pem 3072
openssl rsa -pubout -in privatekey.pem -out publickey.pem -outform PEM
```

IBKR issues a client ID and a key ID in return. The key ID is how IBKR knows which registered public key verifies your signature, so it changes when you rotate the key and the client ID does not.

```csharp
.UseOAuth2(o =>
{
    o.ClientId        = "...";     // issued at registration
    o.ClientKeyId     = "...";     // identifies the registered public key
    o.Credential      = "...";     // the IBKR username the session is for
    o.ClientIpAddress = "...";     // your public egress address
    o.UsePrivateKeyFile("/secrets/ibkr-oauth2.pem");   // the private half of the pair above
})
```

`Scope` defaults to `sso-sessions.write`, which is what establishing a brokerage session requires.

`ClientIpAddress` has no default. IBKR validates the claim against the address the request actually arrives from, and its reference implementation discovers it by calling a third-party lookup service — not something this library will do on your behalf unasked. Set it, or supply `ClientIpAddressResolver`.

### OAuth 1.0a

**Getting credentials** depends on which of IBKR's two categories you fall into, and they are not the same process.

*First party* means you are trading your own or your institution's capital — a financial advisor, a hedge fund, an organisation. Email [apiintegration@interactivebrokers.com](mailto:apiintegration@interactivebrokers.com) answering three questions: what you intend to do with OAuth access, which accounts will use it, and whether the client application is being built in-house or by a third-party developer.

*Third party* means you are offering trading to people outside your organisation — a robo-advisor, a public app, an auto-trader. That is [a much longer road](https://ibkrcampus.com/docs/web-api/authentication/oauth-1a/third-party-oauth/registration-process): IBKR estimates 2–3 weeks of vetting, 3–6 weeks of compliance review and 3–5 weeks of legal and key generation, and expects a finished, publicly documented product before it starts. Contact [api-solutions@interactivebrokers.com](mailto:api-solutions@interactivebrokers.com).

Approved first parties get a link to IBKR's Self-Service Portal, which generates the consumer key, the encryption keys and the access token pair. The Diffie-Hellman prime is issued alongside the consumer key.

> **A newly registered consumer key does not work until after midnight** — New York, Zug or Hong Kong, whichever region you are in. Used before that reset it returns `401 Invalid Consumer`, which looks exactly like a signing bug and is not one.

```csharp
.UseOAuth1a(o =>
{
    o.ConsumerKey        = "...";   // Self-Service Portal
    o.Realm              = OAuth1aOptions.LimitedPoaRealm;  // TestRealm for TESTCONS
    o.AccessToken        = "...";   // Self-Service Portal
    o.AccessTokenSecret  = "...";   // base64, still encrypted
    o.DiffieHellmanPrime = "...";   // hex, issued with the consumer key
    o.UseEncryptionKeyFile("/secrets/ibkr-encryption.pem"); // decrypts the token secret
    o.UseSignatureKeyFile("/secrets/ibkr-signature.pem");   // signs the handshake
})
```

The encryption and signing keys are different keys. Swapping them produces a live session token that fails IBKR's signature check, which the client reports rather than using. The generator is fixed at 2 and defaulted accordingly; the prime is read as hexadecimal, with or without a `0x` prefix, and must match IBKR's server-side value exactly or the shared secret cannot be derived.

The library performs the whole handshake — RSA-decrypting the access token secret, the Diffie-Hellman exchange, deriving the live session token and validating it — on first use, then signs each request with the resulting token and renews it when the expiry IBKR returned passes — documented as 24 hours.

## Things about IBKR that will otherwise surprise you

**Sessions are two-tiered.** An outer read-only session gates every request but only reaches non-`/iserver` endpoints. A separate *brokerage* session gates trading, market data and everything else behind `/iserver`. A username may hold only one brokerage session at a time across all platforms, so logging into Trader Workstation displaces one held here. `EnsureBrokerageSessionAsync` establishes it; `BrokerageSessionStatus` keeps IBKR's four flags separate, and `Established` — not `Authenticated` — is the one to gate trading on.

**Sessions time out, and then they expire.** After roughly five idle minutes IBKR drops the session; `AddBrokerageSessionKeepAlive()` pings `/tickle` every 60 seconds to stop that, and without it you call `IIbkrSessionManager.KeepAliveAsync` yourself. Keeping it alive only buys 24 hours, though — a session expires outright at midnight in New York, Zug or Hong Kong, whichever you connect nearest to, so a long-running process has to be able to establish a new one rather than assuming the keep-alive is enough.

**The first market data snapshot returns nothing.** IBKR treats it as a pre-flight that starts the backend streaming the instrument; snapshots are read from those open streams, not from a cache. Send the pre-flight with every field you will later want, then ask again. Each subscribed instrument consumes one of your market data lines (100 by default), so unsubscribe when you are done.

**Call `/portfolio/accounts` first.** Other `/portfolio` endpoints return empty or stale data for an account until it has been listed. IBKR does not report an error, which makes this a slow thing to diagnose.

**An order reply message is not a rejection.** Submission can answer with a prompt IBKR wants confirmed — usually a precautionary limit configured on your username. `SubmitAsync` returns `OrderSubmissionResult.ReplyRequired`, and the default `OrderReplyPolicy.Manual` leaves the decision to you, because these prompts carry margin, liquidity and price-constraint warnings. `OrderReplyPolicy.AutoConfirm` is opt-in. To stop being asked at all, suppress the message categories at the start of the session with `SuppressMessagesAsync`.

**Rate limits are enforced client-side by default.** IBKR caps requests at 10/second per username and applies much tighter per-endpoint limits — `/iserver/scanner/params` allows one request per fifteen minutes. Exceeding them puts your IP in a ten-minute penalty box, and repeat violations can get it blocked, so the client paces requests rather than reacting to a `429`. Waits longer than `RateLimiting.MaxWait` (30 seconds) throw instead of blocking silently.

**A `429` is fed back into the pacing, not just reported.** Parts of the limits table are inference — `/pa/allperiods` appears in no IBKR limits table at all and is paced with its neighbours — so a rejection is the only evidence the client gets that it was wrong about an endpoint. It holds that endpoint for the `Retry-After` window IBKR named before sending to it again; IBKR does not document sending that header, so where it is absent the endpoint's own published window is served again, or `RateLimiting.DefaultRetryAfter` (5 seconds) when nothing covers the path. The hold expires rather than rewriting the table, because a `429` can equally come from a second process on your username or an address you share. It covers only the endpoint that was rejected: one tight limit should not stall every other call in the process.

**A market scanner selects contracts; it does not report the numbers it ranked them by.** Each row carries a `scan_data` field holding the ranked value, and IBKR's published example shows it on every row — but a live gateway omitted it from all fifty rows of a Top % Gainers scan, both pre-market and during regular trading hours, returning only the contracts and the column heading. Read quotes for the returned conids if the numbers matter. `/iserver/scanner/params` is separately awkward: it is 200 KB of reference data behind the tightest limit in the API, one request per fifteen minutes, so fetch it once and hold it. A `combo` filter's choices come back carrying nothing but which one is the default — no value, no label — so what to send for one has to be read out of Trader Workstation.

**A number that does not apply comes back as an empty string.** A market order's `limit_price`, a filled order's `price` — IBKR sends `""` rather than `null` or nothing at all, and `"None"` and `"N/A"` appear in the same role elsewhere. Every nullable numeric property absorbs these as `null`, so one inapplicable field does not fail the whole response. A non-nullable one refuses instead of reading a plausible, wrong zero.

**IBKR's list of notification type codes is not the list it sends.** The reference publishes twenty-three `typecode` values as a closed enum. A live gateway returns thirty categories from `/fyi/settings`, eleven of which — `OI`, `AA`, `BR`, `EH`, `NS`, `NP`, `PF`, `PC`, `SP`, `SL`, `TP` — are on no list anywhere, while four that are documented never appear. IBKR's *own* example response for that endpoint contains `PF`. So `NotificationTypeCode` is an open struct with the documented codes as static members, not an enum: an enum would turn every unlisted code into a failure of the whole response. The same group documents a notification's read flag as a string and sends a number, documents an `HT` field it never sends, and sends an `SS` field it never documented.

**Two FYI endpoints answer a cold call by saying they are not ready.** `/fyi/unreadnumber` and `/fyi/notifications` return `503 Service Unavailable`, or `423 Locked` with the body `{"status":"waiting for reply"}`, while the gateway fetches from IBKR — then serve the data on a later call. Neither status is documented on either endpoint. The client does not retry for you: every endpoint in the group is limited to one request per second, and spending a caller's rate budget on a condition they cannot see is not a decision a library should make quietly. `IbkrApiException.StatusCode` carries the status, so the policy is yours.

**Event contract discovery has three kinds of identifier and they are not interchangeable.** A market in `/forecast/category/tree` carries both a `conid` and a `product_conid`; only the first is accepted by `/forecast/contract/market`, and passing the second answers `404`. That endpoint then lists the individual contracts, whose conids — different again — are what `/forecast/contract/details`, `/rules` and `/schedules` take. Nothing in the reference says which identifier belongs where, and every wrong combination fails the same silent way. The client's parameter names and doc comments say which is which, and the live sweep walks the chain top to bottom rather than hard-coding a conid, so a run proves the three endpoints still agree.

**The event contract group sends five fields it never documented, and contradicts itself on a sixth.** `is_restricted` appears on every category and every market; `party` and `product_conid` appear on a contract; a `price_increments` table appears beside the single `price_increment` the docs describe. And `strike` is documented as an integer but arrives as `1.0` — on a market whose outcomes are candidates rather than numbers, the strike is an ordinal standing in for a name, and `strike_label` is the field worth reading. It is modelled as a `decimal` so a genuinely fractional strike is not truncated on the way in.

**Alerts can be read and destroyed but not created.** IBKR publishes five alert endpoints and no way to make an alert: they have to be built in Trader Workstation or Client Portal, which makes `DeleteAsync` one-way. To stop an alert firing without losing it, deactivate it. The reading is awkward too. `GET /iserver/account/alert/{alertId}` takes a `type` query parameter that the reference's path signature does not show, whose only allowed value is `Q`, and omitting it is a `400` — the client always sends it, so callers never meet this. Worse, an unknown alert is reported as **`200 OK`** carrying `{"error": "Alert with order ID=... not found."}`, so nothing about the transport says the read failed and every field would deserialize to `null`; `GetDetailsAsync` checks the body and throws.

**The mobile trading assistant alert's `order_id` is not an identifier.** Every username has exactly one MTA alert, and IBKR documents its order id as being reissued when the alert is modified. In fact a live gateway hands out a new one on every read — three consecutive calls returned 487543692, 487543693 and 487543694 — because the value is drawn from the account's order sequence per request. No endpoint accepts it: pass it to the alert details endpoint and you get "not found". `tool_id` is the stable identity. The live sweep reads the endpoint twice and prints both ids, so this stays checked rather than remembered.

**An alert is an order, which is why it has a time in force.** TWS builds alerts out of the order system, so an alert carries `tif`, an `order_status` and an `order_id`, and this library types the identifier as an `OrderId`. IBKR then documents `order_status` twice over as a closed set — "Always returns 'Presubmitted'", with `Presubmitted` and `Submitted` as the allowed values — and a live gateway answers `Inactive`. It is a string here for that reason. Most of the rest of the alert is yes-or-no flags that the reference types as `long`; live responses mix real JSON booleans in among the `1`s and `0`s on the same object.

**PortfolioAnalyst's all-periods response hides its data under the account number.** IBKR documents seven fields for `POST /pa/allperiods` — `pm`, `nd`, `id`, `currencyType`, `view`, `included`, `rc` — and not one of them is the performance. The figures arrive under a property named after the account: alongside those seven sits `"DU1234567": { "1D": {…}, "1Y": {…}, "baseCurrency": "USD", "start": "20250908" }`, in which the period names are themselves property names sitting beside ordinary fields. A model bound to the documented schema deserializes cleanly and returns nothing of value, which is the worst way for this to fail. `PerformanceAllPeriods` is read by a converter instead: any property holding an object is an account, and inside it any property holding an object is a period. Everything IBKR does document is a string, a number or an array, so the rule separates them cleanly.

**PortfolioAnalyst dates are three different things, and the useful one is undocumented.** In a performance series `dates` runs parallel to the numbers, and its encoding follows the sibling `freq`: `"20260908"` while that is `D`, `"202609"` while it is `M` — both in the same response, since a one-month request answers with daily NAV and monthly interval returns. The array is therefore exposed as text; parsing it eagerly would mean either failing on the monthly series or inventing a day of the month IBKR did not send. Transactions are worse. The documented `date` is Java's default rendering of a date, `Wed Nov 05 00:00:00 EST 2025`, whose zone is an abbreviation rather than an identifier and so is ambiguous across the world's zones — but every transaction also carries an undocumented `rawDate` of `20251105`. `Transaction.Date` binds the undocumented field and keeps the documented one as `DateText`. The all-periods `lastSuccessfulUpdate` adds a format nothing else on the API uses, `2026-09-08 17:33:08`, with no stated zone; it is read as UTC because two requests fired at 17:33:08 and 17:48:45 UTC, from a host an hour ahead, came back reading exactly those times. Which also says the figures are recomputed per call, so the field reports when the call was answered rather than how stale the answer is. The live sweep re-checks the zone on every run.

**Three PortfolioAnalyst fields mean something other than what they say.** `nd` is described on every endpoint that sends it as "the total data points", and is nothing of the sort: a one-month performance request answered `33` against 22 points in its series, a one-year request `366` against 262, and a 365-day transaction request `366` against seven transactions. Every one of those is near the calendar width of the window instead — but not by a rule that survives both performance samples, since the year's `366` is its start and end counted inclusively and the month's `33` is one more than the same sum. It is called `DayCount` here and documented as approximate; the length of a series is read from the series. The realized-pnl `side` is documented as `L` for LOSS and `G` for GAIN; a live gateway sent `L` on entries whose amount was `+92.71` and `+20.90`, with an undocumented `positionSide: "long"` on the same entries — so it is a string, and the sign of the amount is what tells a gain from a loss. And `period` offers windows the endpoint does not have: the API reference lists `3M`, `6M` and `12M`, none of which the endpoint guide mentions, and a live gateway answers `400 Bad Request: Invalid period: 3M` — validated and rejected by name rather than silently defaulted. `PerformancePeriod` therefore models the six the gateway accepts — `1D`, `7D`, `MTD`, `1M`, `YTD`, `1Y` — which is the endpoint guide's set and is also the `periods` array `/pa/allperiods` reports for itself. Carrying the reference's three would have handed callers a quarter of an hour's wait to learn the documentation was wrong.

**PortfolioAnalyst rejects requests its own reference calls valid.** `POST /pa/transactions` documents `acctIds`, `conids` and `currency` as optional — `currency` even carries a documented default of `USD` — and offers `{}` as its example request. A live gateway answers a request missing any of the three with `400 Bad Request: acctIds, currency and conids are required`, and because the endpoint allows one request per fifteen minutes, finding that out costs a quarter of an hour before the next attempt. The client sends IBKR's own documented default for `currency` and rejects a missing account or contract as an `ArgumentException` on the spot, so the window is never spent on it. `days` is genuinely optional and stays omitted.

**Time is encoded inconsistently, which is why this library uses NodaTime.** The same API sends epoch seconds, epoch milliseconds, epoch milliseconds inside a JSON string, `YYYYMMDD-hh:mm:ss`, `YYMMDDhhmmss`, `yyyyMMdd` and `HHmm` — sometimes two encodings of one value on the same object. Each field declares the converter for its documented format, so `ledger.RetrievedAt` (seconds) and `trade.TradeTime` (milliseconds) both arrive as a correct `Instant`. Where IBKR sends the same moment twice — `trade_time` beside `trade_time_r`, `lastExecutionTime` beside `lastExecutionTime_r` — a real fill on a live gateway confirms the text halves are UTC and that both decodings land on the same instant. Trading schedules go further: opening and closing times are `LocalTime` values in the venue's own zone, reported as an IANA identifier, and `tradingScheduleDate` can mean "any Saturday" rather than a date — see `TradingScheduleDate`. Event contracts add a ninth encoding that nothing else on the API uses: a twelve-hour clock, `4:15 PM`, which is kept in its own converter rather than merged into the `HHmm` one so that a schedule which started sending `0415` fails loudly instead of being read as a quarter past four in the morning. Their zone is `US/Central` — a TZDB backward-compatibility link, not the canonical `America/Chicago` the rest of the API would send — which NodaTime's TZDB provider resolves and which a test pins. PortfolioAnalyst adds a tenth: `yyyy-MM-dd HH:mm:ss`, ISO 8601 but for the space where the `T` belongs, and the only place on the API where IBKR punctuates a date at all.

## Endpoints not yet modelled

`IIbkrApiClient` reaches anything this library has not covered yet, keeping authentication, rate limiting and error mapping:

```csharp
var watchlists = await apiClient.SendAsync<JsonElement>(
    IbkrRequest.Get("/v1/api/iserver/watchlists"), ct);
```

## Working on this repository

```bash
dotnet build IbkrDotNet.slnx -c Release
dotnet test --solution IbkrDotNet.slnx -c Release
```

The documentation site is DocFX, pinned in `.config/dotnet-tools.json`. It renders this file alongside thirteen guides and the API reference generated from the XML doc comments:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

CI builds it with `--warningsAsErrors`, so a link that stops resolving — a heading here renamed, a guide moved — fails at the push that broke it rather than becoming a dead link nobody reports. `docs/_site/` and the generated `docs/api/*.yml` are gitignored; the site is a build artifact.

Both packages carry a transcript of their public surface under `tests/*/PublicApi/`, and a test fails when the assembly stops matching it. Both are published, so a signature that changes is somebody's compile error; nothing else in the suite would notice a widened parameter or a type that stopped being sealed. When a change is intended, the failure writes the new surface beside the approved one as `.received.txt` — read the diff, then replace the approved file with it.

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

The PortfolioAnalyst checks read and never write, but they are the slowest thing in the sweep to repeat: each of the four endpoints allows one request per fifteen minutes. The client will not block that long, so a second run inside the window reports them as skips saying how much of it is left, rather than stalling. Two of the four also re-prove claims the library documents rather than trusting them — that `lastSuccessfulUpdate` is UTC, and that `nd` counts calendar days and not data points — so a documentation comment that drifts away from the API is caught by a run instead of by a caller.

No credential is read or printed: the gateway holds the login, and the account identifier is discovered at runtime and masked on the way out, so the output can go straight into a bug report.

## Contributing

See [CONTRIBUTING.md](https://github.com/jerbersoft/ibkrdotnet/blob/master/CONTRIBUTING.md). Every test runs
offline against recorded fixtures, so a fresh clone goes green with no IBKR account, gateway or key.

## Security

**Do not report a security problem in a public issue.** This is a brokerage client: the material it handles —
RSA private keys, access-token secrets, live session tokens — is worth more than most. See
[SECURITY.md](https://github.com/jerbersoft/ibkrdotnet/blob/master/SECURITY.md) for the private channel.

## License

MIT. See [LICENSE](https://github.com/jerbersoft/ibkrdotnet/blob/master/LICENSE).
