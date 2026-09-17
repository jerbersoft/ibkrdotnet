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

## A bar's volume is not what IBKR sends

IBKR divides every bar's volume by a factor it puts on the envelope, and the factor is not constant: a live
gateway answered `volumeFactor: 40` where IBKR's own published example uses `100`.

```csharp
bars.VolumeFactor        // 40
bars.Bars[^1].RawVolume  // 142573.625 — the wire's 'v'
bars.Bars[^1].Volume     // 5702945    — shares
```

`Volume` is the share count with the factor put back. `RawVolume` is the figure as sent, kept because the
envelope's own `high` and `low` summary strings quote the volume unscaled.

The wire's number looks like a volume and compares sensibly against other numbers read the same way, so reading
it as one survives review — until it meets a volume from anywhere else, such as the day's volume in a snapshot,
and the two are a factor of 40 apart.

Prices are not scaled by anything. `priceFactor` divides the `high` and `low` summary strings, not a bar's `o`,
`h`, `l` and `c`, which arrive as real prices.

## Bar timestamps

Bars carry `Instant` values decoded from whichever encoding IBKR used for that field — epoch seconds in some
places, epoch milliseconds in others, `YYYYMMDD-hh:mm:ss` in others again. You do not have to know which; the
converter on each property does. [Dates and times](dates-and-times.md) is the full account of why that matters.

## Rate limits

`/iserver/marketdata/snapshot` allows 10 requests per second, which the client paces. That is generous by IBKR's
standards — most of the tight limits are elsewhere — but the global cap of 10 requests per second per username
applies on top, so a snapshot loop can exhaust the whole budget by itself. See [Rate limits](rate-limits.md).

## Streaming

The same quote, pushed rather than polled. `IMarketDataStreamClient` runs over IBKR's WebSocket and hands back
each update as IBKR sends it:

```csharp
await foreach (var quote in ibkr.MarketDataStream.SubscribeAsync(conId, MarketDataField.TopOfBook, cancellationToken: ct))
{
    Console.WriteLine($"{quote.LastPrice} {quote.BidPrice}/{quote.AskPrice} at {quote.UpdatedAt}");
}
```

Nothing is sent until the loop starts. The first read opens the socket if it is not open — which needs the
brokerage session, so `EnsureBrokerageSessionAsync` applies here as everywhere else behind `/iserver` — and sends
`smd` for the instrument. Leaving the loop, whether by `break`, cancellation or an exception, sends `umd`, which
releases the instrument's market data line. Read a stream for exactly as long as you want it: every open stream
holds one of the account's lines, the same 100 the snapshot endpoint draws on.

`MarketDataUpdate` is the snapshot's shape — data points keyed by tick number, read through the same named
accessors and the same `MarketDataField` constants — plus the `topic` that routed it and `ReceivedAt`, when the
transport read it. IBKR does not promise every requested field in every message. An accessor returning `null`
means the field was not in *this* message, not that it has no value; keep the last value you saw.

**IBKR ends a stream fifteen minutes after it was requested.** The client re-sends the request every ten minutes
(`Streaming.MarketDataRenewalInterval`) for as long as the loop is running, so a long read sees no gap and you do
nothing. An interval longer than IBKR's limit means a gap every cycle.

**Updates arrive at most every 500 ms**, and a reader that falls behind is handed the latest message rather than
a backlog. That is the transport's default buffer of one message with drop-oldest, which is the right rule for a
quote; [Configuration](configuration.md#the-settings) has the knobs.

**A socket that drops does not end the loop.** The transport reopens it with backoff and re-sends the request, so
the loop sees a pause and then updates again. Set `Streaming.Reconnect = false` to have the loop end with an
`IbkrStreamingException` instead.

**Two keep-alives, not one.** The transport sends `tic` every 30 seconds to keep the socket alive. That does not
keep the brokerage session behind it alive; the REST `/tickle` still has to run, so
`AddBrokerageSessionKeepAlive()` is as necessary with a stream open as without. [Sessions](sessions.md) covers
what it is keeping alive.

`exchange` names a data source — `SubscribeAsync(conId, exchange: "ARCA", ...)` sends `smd+CONID@ARCA` — and
SMART is the default. The REST `UnsubscribeAsync` and `UnsubscribeAllAsync` close the same backend streams, so a
stream closed that way goes quiet until its next renewal requests it again.

Against the Client Portal Gateway the socket meets the same self-signed certificate `HttpClient` does, and the
handler you configured for `HttpClient` does not cover the WebSocket upgrade. `Streaming.ConfigureClientWebSocket`
is where the socket's certificate validation is relaxed; the console sample shows the loopback-only version.

### Other topics

Market data is the one topic with a typed client. The rest of IBKR's streaming surface — orders, trades,
profit and loss, the account ledger and summary, the price ladder, historical bars — is reachable through the
transport the market data client is built on, keeping authentication, keep-alive and reconnection:

```csharp
var request = StreamingSubscriptionRequest.Solicited(
    StreamingTopics.OrderUpdates, parameters: new { filters = new[] { "Submitted", "Filled" } });

await using var orders = await ibkr.Streaming.SubscribeAsync(request, ct);
await foreach (var message in orders.ReadAllAsync(ct))
{
    var body = message.Body;   // the JsonElement as IBKR sent it, or message.Deserialize<T>() into your own record
}
```

`StreamingTopics` names every topic IBKR publishes. A solicited request opens the socket, sends its frame, and
derives its unsubscribe frame by IBKR's convention (`sor` → `uor`); disposing the subscription sends it. IBKR's
unsolicited topics — `system`, `sts`, `act`, `blt` and `ntf` — are read with
`StreamingSubscriptionRequest.Unsolicited`, which only registers to be routed to. IBKR sends the first `system` and `sts` messages on connect, so register
for those *before* the socket opens or you will race them. `ibkr.Streaming.IsBrokerageSessionAuthenticated` and
`LastHeartbeatAt` are the transport's own reading of those two topics, kept whether or not anybody subscribes.

**`ntf` carries two different things.** A notice reports something that happened and asks nothing — warning 118
dating a resting order's automatic cancellation, say. A prompt is a question about an order, one of the same
questions an order submission answers over REST, arriving unsolicited because something other than this client
provoked it. They are separate types under one `Args` list, which IBKR documents as a bare object and a gateway
sends as an array:

```csharp
foreach (var arg in message.Deserialize<StreamingNotification>().Args)
{
    switch (arg)
    {
        case StreamingNotificationArgs.Notice notice:
            logger.LogInformation("IBKR {Id}: {Text}", notice.Id, notice.Text);
            break;

        case StreamingNotificationArgs.Prompt prompt:
            // OrderId is IBKR's own numeric identifier for the order, not the one you supplied, and
            // the answer is one of prompt.Options sent back verbatim.
            await ibkr.Orders.DismissServerPromptAsync(
                prompt.OrderId!.Value, prompt.RequestId!, prompt.Options[0], ct);
            break;
    }
}
```

Answering is a trading decision, so nothing answers one for you. Left alone, the order stands as IBKR already has
it; `SuppressMessagesAsync` takes the prompt's `MessageId` when the answer is always going to be the same one.
