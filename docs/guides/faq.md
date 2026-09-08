# FAQ

## Is this an official Interactive Brokers library?

No. It is an independent client, not affiliated with, endorsed by, or supported by Interactive Brokers. Bugs go
[here](https://github.com/jerbersoft/ibkrdotnet/issues), not to IBKR.

## Which IBKR API is this?

The **Web API Trading** surface — the REST API at `/v1/api`. Not the TWS API (the socket protocol behind
`IBApi`), not FIX, and not the Flex Web Service.

## Can I use it with a free account?

You need **IBKR Pro**. IBKR Lite has no API access. The account also needs funds; an unfunded one authenticates
and then answers most endpoints with nothing.

## Do I have to run the gateway?

For the retail path, yes — a Java process, logged in through a browser, on a machine you control. Both OAuth
mechanisms talk to `api.ibkr.com` directly with no intermediary, but neither is self-serve: see
[Authentication](authentication.md).

## Can I trade my own individual account over OAuth?

Not over OAuth 2.0 — IBKR closes it to individual account structures. OAuth 1.0a's first-party route is worth an
email to `apiintegration@interactivebrokers.com`; the third-party route is measured in months.

## Why NodaTime, and can I opt out?

Because IBKR encodes time ten different ways and sometimes sends two encodings of one value on the same object.
That is the class of bug NodaTime exists to make unrepresentable — see [Dates and times](dates-and-times.md).

You cannot opt out; there is no `DateTime` overload. NodaTime converts at your boundary in one line if your
application speaks BCL types.

## Does it support WebSocket streaming?

No, and it is not planned. Market data is read through the snapshot endpoint over the streams IBKR opens.

## Why does my first market data snapshot come back empty?

By design. The first call is a subscription request that starts IBKR streaming the instrument; the second reads
it. [Market data](market-data.md).

## Why is my order "rejected" with a question?

It is not rejected. `OrderSubmissionResult.ReplyRequired` means IBKR wants something confirmed — usually a
precautionary limit on your username. Confirm it with `ConfirmReplyAsync`, or suppress the categories up front.
[Placing orders](orders.md).

## Can I turn the rate limiting off?

`options.RateLimiting.Enabled = false`. Rarely a good idea: IBKR's response to a breach is a ten-minute penalty
box on your IP, and repeat violations can get it blocked. [Rate limits](rate-limits.md).

## Is it thread-safe?

Yes. The clients are stateless over a shared `HttpClient`; the rate limiters are a singleton and synchronize
internally; the authenticators cache their tokens behind a lock. Register once and inject anywhere.

## Is it AOT- and trimming-friendly?

Response models are `record` types serialized with `System.Text.Json`. The library does not use reflection-based
configuration binding for its own options — durations are bound explicitly — but it has not been through a full
AOT audit, so treat AOT as untested rather than supported.

## How do I reach an endpoint you have not modelled?

`IIbkrApiClient`, which keeps authentication, rate limiting and error mapping:

```csharp
await api.SendAsync<JsonElement>(IbkrRequest.Get("/v1/api/iserver/watchlists"), ct);
```

[Endpoint coverage](endpoint-coverage.md).

## What is not implemented?

The two Financial Advisor groups — allocation management and model portfolios, 18 endpoints between them. They
need an FA master account to verify against. [Endpoint coverage](endpoint-coverage.md).

## Is it on nuget.org?

Not yet. Both packages build and pack from the repository; publishing is deliberately deferred.

## Which .NET version?

`net10.0` only.

## How do I test code that calls this?

Every client is an interface, so substitute it. For testing the library's own time-dependent behaviour, `IClock`
and `IDateTimeZoneProvider` are registered with `TryAdd` and a `FakeClock` slots straight in.

## Where does the measured IBKR behaviour live?

The [reference](../../README.md#things-about-ibkr-that-will-otherwise-surprise-you). It is the most valuable page
in the repository: every entry is something a live gateway did that the documentation does not say.
