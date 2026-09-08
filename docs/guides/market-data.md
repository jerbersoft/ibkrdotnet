# Market data

## The first snapshot returns nothing

This is the one to know before anything else.

```csharp
// Pre-flight. Returns no values — it starts IBKR streaming the instrument in the backend.
await ibkr.MarketData.GetSnapshotAsync([conId], [MarketDataField.LastPrice, MarketDataField.BidPrice], ct);

// Now ask again.
var quote = await ibkr.MarketData.GetSnapshotAsync([conId], [MarketDataField.LastPrice, MarketDataField.BidPrice], ct);
```

IBKR treats the first call as a subscription request, not a query: snapshots are read from open backend streams
rather than from a cache, and the stream does not exist until you ask for it. An empty first response is correct
behaviour, not a failure.

**Send the pre-flight with every field you will later want.** Fields you did not ask for are not being streamed,
so adding one later starts the cycle again for that field.

## Fields are numbers

IBKR identifies market data points by tick number, and the same numbers key the response object.
`MarketDataField` holds them as named constants so calling code does not carry bare integers:

```csharp
MarketDataField.LastPrice      // "31"
MarketDataField.BidPrice       // "84"
MarketDataField.AskPrice       // "86"
MarketDataField.High           // "70"
MarketDataField.ChangePercent  // "83"
```

`LastPrice` can carry a prefix — `C` for a close, `H` for a halt — so it is not always a bare number.

## Market data lines are finite

Each subscribed instrument consumes one of the account's market data lines, 100 by default. They are not released
when your process exits.

```csharp
await ibkr.MarketData.UnsubscribeAsync(conId, ct);
await ibkr.MarketData.UnsubscribeAllAsync(ct);
```

`UnsubscribeAllAsync` in a shutdown path is cheap insurance. A service that subscribes on every request and never
unsubscribes will run out, and the symptom is snapshots that stay empty forever — indistinguishable from the
pre-flight behaviour above.

## Historical bars

```csharp
var bars = await ibkr.MarketData.GetHistoryAsync(
    conId,
    HistoryPeriod.OneMonth,
    BarSize.OneDay,
    exchange: "SMART",
    cancellationToken: ct);
```

`HistoryPeriod` is how far back the request reaches; `BarSize` is the width of each bar. They need not divide
evenly. Both are strongly typed rather than strings — `HistoryPeriod.Days(5)`, `BarSize.Minutes(15)` — and both
parse IBKR's wire spelling if you have one already: `HistoryPeriod.Parse("6m")`, `BarSize.Parse("5min")`.

`startTime` fixes a reference point; leave it unset and the request runs from now, in which case `direction` has
to stay at its default. IBKR permits five concurrent historical data requests.

## Bar timestamps

Bars carry `Instant` values decoded from whichever encoding IBKR used for that field — epoch seconds in some
places, epoch milliseconds in others, `YYYYMMDD-hh:mm:ss` in others again. You do not have to know which; the
converter on each property does. [Dates and times](dates-and-times.md) is the full account of why that matters.

## Rate limits

`/iserver/marketdata/snapshot` allows 10 requests per second, which the client paces. That is generous by IBKR's
standards — most of the tight limits are elsewhere — but the global cap of 10 requests per second per username
applies on top, so a snapshot loop can exhaust the whole budget by itself. See [Rate limits](rate-limits.md).

## Streaming

Out of scope. IBKR's WebSocket surface is not modelled by this library and there are no plans to; the snapshot
endpoint over the streams it opens is what is here.
