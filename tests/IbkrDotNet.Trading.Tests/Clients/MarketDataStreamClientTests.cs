using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Streaming;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class MarketDataStreamClientTests
{
    private static readonly Instant Start = Instant.FromUtc(2024, 4, 8, 16, 41, 51);

    private static readonly ConId Ibm = new(8314);

    // IBKR's own example: smd+8314+{"fields":["31","84","85","86","88","7059"]}
    private static readonly string[] DocumentedFields = ["31", "84", "85", "86", "88", "7059"];

    private const string DocumentedFrame = """smd+8314+{"fields":["31","84","85","86","88","7059"]}""";

    private const string UnsubscribeFrame = "umd+8314+{}";

    private sealed class Harness : IAsyncDisposable
    {
        public Harness(Action<IbkrTradingOptions>? configure = null)
        {
            var options = new IbkrTradingOptions();

            // Kept out of the way unless a test lowers them.
            options.Streaming.KeepAliveInterval = Duration.FromMinutes(5);
            options.Streaming.MarketDataRenewalInterval = Duration.FromMinutes(5);
            options.Streaming.ReconnectDelay = Duration.FromMilliseconds(10);
            options.Streaming.ReconnectMaxDelay = Duration.FromMilliseconds(40);
            configure?.Invoke(options);

            var state = new IbkrSessionState();
            var clock = new FakeClock(Start);
            Connector = new FakeWebSocketConnector();
            Transport = new IbkrStreamingTransport(
                new FakeSessionManager(state, clock),
                state,
                new FakeAuthenticator(),
                Connector,
                Options.Create(options),
                clock,
                NullLogger<IbkrStreamingTransport>.Instance);
            Client = new MarketDataStreamClient(
                Transport,
                Options.Create(options),
                NullLogger<MarketDataStreamClient>.Instance);
        }

        public MarketDataStreamClient Client { get; }

        public IbkrStreamingTransport Transport { get; }

        public FakeWebSocketConnector Connector { get; }

        /// <summary>Waits for the socket the first read opens, and for the request it sends.</summary>
        public async Task<FakeWebSocket> SocketAfterRequestAsync(string frame, CancellationToken cancellationToken)
        {
            var socket = await Connector.WaitForSocketAsync(0, cancellationToken);
            Assert.Equal(frame, await socket.NextSentAsync(cancellationToken));
            return socket;
        }

        public ValueTask DisposeAsync() => Transport.DisposeAsync();
    }

    private static CancellationToken Within(TimeSpan? timeout = null) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken,
            new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10)).Token).Token;

    [Fact]
    public async Task Sends_the_documented_request_on_the_first_read_and_yields_the_documented_response()
    {
        var ct = Within();
        await using var harness = new Harness();

        var updates = harness.Client.SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct);

        // Nothing has been sent, or opened, until the enumeration starts.
        Assert.Empty(harness.Connector.Sockets);

        await using var enumerator = updates.GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        socket.Push(Fixture.ReadText("Responses/websocket/market-data-response.json"));

        Assert.True(await first);
        var update = enumerator.Current;
        Assert.Equal(Ibm, update.ConId);
        Assert.Equal("smd+8314", update.Topic);
        Assert.Equal(189.60m, update.LastPrice);
        Assert.Equal(189.56m, update.BidPrice);
        Assert.Equal(189.61m, update.AskPrice);
        Assert.Equal(200m, update.BidSize);
        Assert.Equal(500m, update.AskSize);
        Assert.Equal(100m, update.GetDecimal(MarketDataField.LastSize));
        Assert.Equal("RpB", update.MarketDataAvailability);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1712596911593), update.UpdatedAt);
        Assert.Equal(Start, update.ReceivedAt);
    }

    [Fact]
    public void Streams_the_top_of_book_fields_when_none_are_given()
    {
        var request = MarketDataStreamClient.CreateRequest(Ibm, fields: null, exchange: null);

        Assert.Equal("""smd+8314+{"fields":["31","84","86","88","85","87"]}""", request.SubscribeFrame);
        Assert.Equal("smd+8314", request.RoutingKey);
        Assert.Equal(UnsubscribeFrame, request.UnsubscribeFrame);
    }

    [Fact]
    public void Names_the_data_source_on_the_target_when_an_exchange_is_given()
    {
        // IBKR: "To specify the exchange, the contract identifier should be modified to conId@EXCHANGE."
        var request = MarketDataStreamClient.CreateRequest(Ibm, ["31"], "ARCA");

        Assert.Equal("""smd+8314@ARCA+{"fields":["31"]}""", request.SubscribeFrame);
        Assert.Equal("smd+8314@ARCA", request.RoutingKey);
        Assert.Equal("umd+8314@ARCA+{}", request.UnsubscribeFrame);
    }

    [Fact]
    public async Task Routes_an_exchange_qualified_stream_by_the_topic_ibkr_restates()
    {
        var ct = Within();
        await using var harness = new Harness();

        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, ["31"], "ARCA", ct)
            .GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync("""smd+8314@ARCA+{"fields":["31"]}""", ct);

        socket.Push("""{"topic":"smd+8314","conid":8314,"conidEx":"8314","31":"1"}""");
        socket.Push("""{"topic":"smd+8314@ARCA","conid":8314,"conidEx":"8314@ARCA","31":"189.60"}""");

        Assert.True(await first);
        Assert.Equal("8314@ARCA", enumerator.Current.ConIdWithExchange);
        Assert.Equal(189.60m, enumerator.Current.LastPrice);
    }

    [Fact]
    public async Task Rejects_bad_fields_at_the_call_rather_than_the_first_read()
    {
        await using var harness = new Harness();

        var empty = Assert.Throws<ArgumentException>(() => harness.Client.SubscribeAsync(Ibm, [], cancellationToken: TestContext.Current.CancellationToken));
        var blank = Assert.Throws<ArgumentException>(() => harness.Client.SubscribeAsync(Ibm, ["31", " "], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("fields", empty.ParamName);
        Assert.Equal("fields", blank.ParamName);
        Assert.Empty(harness.Connector.Sockets);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AR CA")]
    [InlineData("ARCA+")]
    public async Task Rejects_an_exchange_that_cannot_be_framed(string exchange)
    {
        await using var harness = new Harness();

        var ex = Assert.Throws<ArgumentException>(() => harness.Client.SubscribeAsync(Ibm, exchange: exchange, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("exchange", ex.ParamName);
    }

    [Fact]
    public async Task Sends_umd_when_the_reader_stops()
    {
        var ct = Within();
        await using var harness = new Harness();
        var enumerator = harness.Client.SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct).GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);
        socket.Push("""{"topic":"smd+8314","conid":8314,"31":"189.60"}""");
        Assert.True(await first);

        // What a `break` out of `await foreach` does.
        await enumerator.DisposeAsync();

        Assert.Equal(UnsubscribeFrame, await socket.NextSentAsync(ct));
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Sends_umd_when_the_token_is_cancelled_mid_read()
    {
        var ct = Within();
        await using var harness = new Harness();
        using var reader = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, DocumentedFields, cancellationToken: reader.Token)
            .GetAsyncEnumerator(reader.Token);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        await reader.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(UnsubscribeFrame, await socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task Re_requests_the_stream_on_the_renewal_interval()
    {
        var ct = Within();
        await using var harness = new Harness(o =>
            o.Streaming.MarketDataRenewalInterval = Duration.FromMilliseconds(20));
        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct)
            .GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        // IBKR ends a stream 15 minutes after its last request, so the same request goes again on
        // the interval, and the reader is none the wiser.
        Assert.Equal(DocumentedFrame, await socket.NextSentAsync(ct));
        Assert.Equal(DocumentedFrame, await socket.NextSentAsync(ct));

        socket.Push("""{"topic":"smd+8314","conid":8314,"31":"189.60"}""");
        Assert.True(await first);
        Assert.Equal(189.60m, enumerator.Current.LastPrice);
    }

    [Fact]
    public async Task Survives_the_socket_dropping_and_keeps_renewing_on_the_new_one()
    {
        var ct = Within();
        await using var harness = new Harness(o =>
            o.Streaming.MarketDataRenewalInterval = Duration.FromMilliseconds(30));
        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct)
            .GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var lost = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        lost.Drop();

        // The transport re-sends the request on the new socket; the renewal timer sends it again.
        var replacement = await harness.Connector.WaitForSocketAsync(1, ct);
        Assert.Equal(DocumentedFrame, await replacement.NextSentAsync(ct));
        Assert.Equal(DocumentedFrame, await replacement.NextSentAsync(ct));

        replacement.Push("""{"topic":"smd+8314","conid":8314,"31":"189.60"}""");
        Assert.True(await first);
        Assert.False(first.IsFaulted);
    }

    [Fact]
    public async Task Ends_with_the_transport_failure_when_the_socket_is_lost_and_reconnect_is_off()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.Reconnect = false);
        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct)
            .GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        socket.Drop();

        var ex = await Assert.ThrowsAsync<IbkrStreamingException>(() => first);
        Assert.Contains("reconnection is disabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_marker_prefixed_price_like_the_snapshot_does()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var enumerator = harness.Client
            .SubscribeAsync(Ibm, DocumentedFields, cancellationToken: ct)
            .GetAsyncEnumerator(ct);
        var first = enumerator.MoveNextAsync().AsTask();
        var socket = await harness.SocketAfterRequestAsync(DocumentedFrame, ct);

        // 'C' marks a previous close, as it does on the snapshot endpoint.
        socket.Push("""{"topic":"smd+8314","conid":8314,"31":"C189.60"}""");

        Assert.True(await first);
        Assert.Equal(189.60m, enumerator.Current.LastPrice);
        Assert.Equal("C189.60", enumerator.Current.GetString(MarketDataField.LastPrice));
        Assert.Null(enumerator.Current.BidPrice);
    }
}
