using System.Text.Json;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Models.Streaming;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Streaming;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The WebSocket against a running gateway: open it, read what IBKR sends on connect, ping it,
/// stream one stock until the first quote arrives, wait for a heartbeat, and close it.
/// </summary>
/// <remarks>
/// The upgrade request is the part no fake reproduces: which headers the gateway insists on,
/// whether the session cookie from <c>/tickle</c> is accepted, and what actually arrives in the
/// first second. The market data check adds the one thing the fixtures cannot: a real
/// <c>smd</c> response, printed in full so it can be kept as a live fixture.
/// </remarks>
internal static class StreamingChecks
{
    private static readonly JsonSerializerOptions Capture = new() { WriteIndented = true };

    public static async Task RunAsync(
        IIbkrStreamingTransport transport,
        IMarketDataStreamClient marketData,
        ConId conId,
        Probe probe,
        CancellationToken cancellationToken)
    {
        probe.Group("Streaming (WebSocket)");

        // Registered before the socket opens. IBKR sends both on connect, and a listener registered
        // afterwards would race the messages it is waiting for.
        await using var system = await transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited(StreamingTopics.System), cancellationToken);
        await using var status = await transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited(StreamingTopics.AuthenticationStatus), cancellationToken);

        await probe.RunAsync("GET  /v1/api/ws (upgrade)", async () =>
        {
            await transport.ConnectAsync(cancellationToken);
            var first = await ReadWithinAsync(system, TimeSpan.FromSeconds(10), cancellationToken);
            var message = first.Deserialize<StreamingSystemMessage>();
            return $"state={transport.State} confirmed={message.Success is { Length: > 0 }}";
        });

        await probe.RunAsync("sts  (authentication status)", async () =>
        {
            var message = (await ReadWithinAsync(status, TimeSpan.FromSeconds(10), cancellationToken))
                .Deserialize<StreamingAuthenticationStatus>();
            return $"authenticated={message.Args?.Authenticated} competing={message.Args?.Competing} " +
                   $"(transport sees {transport.IsBrokerageSessionAuthenticated})";
        });

        await probe.RunAsync("tic  (keep-alive)", async () =>
        {
            await transport.SendAsync(StreamingTopics.KeepAlive, cancellationToken);
            return "sent";
        });

        await probe.RunAsync("smd  (market data)", async () =>
        {
            if (conId.Value == 0)
            {
                throw new SkipCheckException("No instrument to stream; the contract search did not find one.");
            }

            // One stock, top of book, until the first update. Leaving the loop disposes the
            // enumerator, which sends umd and releases the market data line.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            MarketDataUpdate? first = null;
            try
            {
                await foreach (var update in marketData.SubscribeAsync(conId, MarketDataField.TopOfBook, cancellationToken: linked.Token))
                {
                    first = update;
                    break;
                }
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"No market data update for {conId} arrived within 15s. Does the account have market data for it?");
            }

            if (first is null)
            {
                throw new InvalidOperationException("The stream ended before an update arrived.");
            }

            // The message as IBKR sent it, for a .live.json fixture beside market-data-response.json.
            // It carries no account data.
            Console.WriteLine("      first update, as received:");
            foreach (var line in JsonSerializer.Serialize(first, Capture).Split(System.Environment.NewLine))
            {
                Console.WriteLine($"      {line}");
            }

            return $"last={first.LastPrice} bid={first.BidPrice} ask={first.AskPrice} " +
                   $"({first.MarketDataAvailability}) updated={first.UpdatedAt}";
        });

        await probe.RunAsync("system (heartbeat)", async () =>
        {
            // One every ten seconds while the socket is healthy, so this is the check that the
            // read loop is still being fed after the burst on connect.
            StreamingSystemMessage message;
            do
            {
                message = (await ReadWithinAsync(system, TimeSpan.FromSeconds(25), cancellationToken))
                    .Deserialize<StreamingSystemMessage>();
            }
            while (!message.IsHeartbeat);

            return $"hb={message.Heartbeat} lastHeartbeatAt={transport.LastHeartbeatAt}";
        });

        await probe.RunAsync("close", async () =>
        {
            await transport.CloseAsync(cancellationToken);
            return $"state={transport.State}";
        });
    }

    private static async Task<StreamingMessage> ReadWithinAsync(
        StreamingSubscription subscription,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await subscription.ReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No '{subscription.Request.Topic}' message arrived within {timeout.TotalSeconds:0}s.");
        }
    }
}
