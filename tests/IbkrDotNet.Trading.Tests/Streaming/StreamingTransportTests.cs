using System.Net.WebSockets;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Streaming;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Streaming;

public class StreamingTransportTests
{
    private static readonly Instant Start = Instant.FromUtc(2024, 4, 8, 16, 41, 51);

    private static readonly string[] LastAndBid = ["31", "84"];

    private static readonly StreamingSubscriptionRequest IbmTopOfBook =
        StreamingSubscriptionRequest.Solicited("smd", "8314", new { fields = LastAndBid });

    private static readonly StreamingSubscriptionRequest AppleTopOfBook =
        StreamingSubscriptionRequest.Solicited("smd", "265598", new { fields = LastAndBid });

    private const string IbmFrame = """smd+8314+{"fields":["31","84"]}""";

    /// <summary>What a gateway answers the upgrade with, before the transport may write anything.</summary>
    private const string Confirmation = """{"topic":"system","success":"U1234567"}""";

    private sealed class Harness : IAsyncDisposable
    {
        public Harness(Action<IbkrTradingOptions>? configure = null, StreamingCredential? credential = null)
        {
            var options = new IbkrTradingOptions();

            // Kept out of the way unless a test lowers them.
            options.Streaming.KeepAliveInterval = Duration.FromMinutes(5);
            options.Streaming.ReconnectDelay = Duration.FromMilliseconds(10);
            options.Streaming.ReconnectMaxDelay = Duration.FromMilliseconds(40);
            configure?.Invoke(options);

            var state = new IbkrSessionState();
            Clock = new FakeClock(Start);
            Session = new FakeSessionManager(state, Clock);
            Connector = new FakeWebSocketConnector();
            Transport = new IbkrStreamingTransport(
                Session,
                state,
                new FakeAuthenticator(credential),
                Connector,
                Options.Create(options),
                Clock,
                Logger);
        }

        public IbkrStreamingTransport Transport { get; }

        public FakeLogger<IbkrStreamingTransport> Logger { get; } = new();

        public FakeWebSocketConnector Connector { get; }

        public FakeSessionManager Session { get; }

        public FakeClock Clock { get; }

        public FakeWebSocket Socket => Connector.Socket;

        public ValueTask DisposeAsync() => Transport.DisposeAsync();
    }

    private static CancellationToken Within(TimeSpan? timeout = null) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken,
            new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10)).Token).Token;

    [Fact]
    public async Task Opens_the_socket_with_the_session_cookie_user_agent_and_origin()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.UserAgent = "contoso-trader/2.1");

        await harness.Transport.ConnectAsync(ct);

        var request = harness.Connector.LastRequest;
        Assert.Equal(new Uri("wss://localhost:5000/v1/api/ws"), request.Address);
        Assert.Equal($"api={FakeSessionManager.DocumentedToken}", request.Headers["Cookie"]);
        Assert.Equal("contoso-trader/2.1", request.Headers["User-Agent"]);
        Assert.Equal("https://localhost:5000", request.Headers["Origin"]);
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
        Assert.Equal(1, harness.Session.EnsureCalls);
    }

    [Fact]
    public async Task Carries_the_mechanism_credential_as_a_query_parameter()
    {
        var ct = Within();
        await using var harness = new Harness(
            o => o.Environment = IbkrEnvironment.Production,
            new StreamingCredential("bearer_token", "SESSION/TOKEN"));

        await harness.Transport.ConnectAsync(ct);

        var request = harness.Connector.LastRequest;
        Assert.Equal("wss://api.ibkr.com/v1/api/ws?bearer_token=SESSION%2FTOKEN", request.Address.ToString());

        // The cookie goes with the query parameter, not instead of it.
        Assert.Equal($"api={FakeSessionManager.DocumentedToken}", request.Headers["Cookie"]);
    }

    [Fact]
    public async Task Connecting_twice_opens_one_socket()
    {
        var ct = Within();
        await using var harness = new Harness();

        await harness.Transport.ConnectAsync(ct);
        await harness.Transport.ConnectAsync(ct);

        Assert.Single(harness.Connector.Sockets);
    }

    [Fact]
    public async Task Refuses_to_open_without_an_established_brokerage_session()
    {
        var ct = Within();
        await using var harness = new Harness();
        harness.Session.Ready = false;

        var ex = await Assert.ThrowsAsync<IbkrAuthenticationException>(() => harness.Transport.ConnectAsync(ct));

        Assert.Contains("brokerage session", ex.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Connector.Requests);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);
    }

    [Fact]
    public async Task Refuses_to_open_without_a_session_token_to_present()
    {
        var ct = Within();
        await using var harness = new Harness();
        harness.Session.Token = string.Empty;

        var ex = await Assert.ThrowsAsync<IbkrAuthenticationException>(() => harness.Transport.ConnectAsync(ct));

        Assert.Contains("api= cookie", ex.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Connector.Requests);
    }

    [Fact]
    public async Task Reports_a_socket_that_cannot_be_opened_without_leaking_the_credential()
    {
        var ct = Within();
        await using var harness = new Harness(credential: new StreamingCredential("bearer_token", "SECRET"));
        harness.Connector.FailNextConnect(new WebSocketException(WebSocketError.Faulted, "refused"));

        var ex = await Assert.ThrowsAsync<IbkrStreamingException>(() => harness.Transport.ConnectAsync(ct));

        Assert.Contains("wss://localhost:5000/v1/api/ws", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", ex.Message, StringComparison.Ordinal);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);
    }

    [Fact]
    public async Task Sends_the_subscribe_frame_and_routes_messages_by_topic()
    {
        var ct = Within();
        await using var harness = new Harness();

        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);

        Assert.Equal(IbmFrame, await harness.Socket.NextSentAsync(ct));

        harness.Socket.Push("""{"topic":"smd+265598","31":"192.26"}""");
        harness.Socket.Push(Fixture.ReadText("Responses/websocket/market-data-response.json"));

        var message = await subscription.ReadAsync(ct);

        Assert.Equal("smd+8314", message.Topic);
        Assert.Equal(8314, message.Body.GetProperty("conid").GetInt32());
        Assert.Equal("189.60", message.Body.GetProperty("31").GetString());
        Assert.Equal(Start, message.ReceivedAt);
    }

    /// <remarks>
    /// The Client Portal Gateway sends every frame as binary, session confirmations and heartbeats
    /// included, so a transport that routes only text frames delivers nothing at all against a real
    /// gateway while looking connected and healthy (#69).
    /// </remarks>
    [Fact]
    public async Task Routes_a_binary_message_the_way_it_routes_a_text_one()
    {
        var ct = Within();
        await using var harness = new Harness();

        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        Assert.Equal(IbmFrame, await harness.Socket.NextSentAsync(ct));

        harness.Socket.PushBinary("""{"topic":"smd+8314","31":"189.60"}""");

        var message = await subscription.ReadAsync(ct);

        Assert.Equal("smd+8314", message.Topic);
        Assert.Equal("189.60", message.Body.GetProperty("31").GetString());
    }

    /// <remarks>
    /// A gateway completes the upgrade before its own upstream session exists and silently discards
    /// anything written in that gap, so the first subscription on every connection delivered nothing
    /// for the life of that connection, with nothing to say so (#73).
    /// </remarks>
    [Fact]
    public async Task Holds_a_subscription_until_ibkr_confirms_the_session()
    {
        var ct = Within();
        await using var harness = new Harness();
        harness.Connector.Confirmation = null;

        var subscribing = harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        var socket = await harness.Connector.WaitForSocketAsync(0, ct);

        // The socket is open, and the frame is not on it.
        await Task.Delay(100, ct);
        Assert.Empty(socket.Sent);
        Assert.False(subscribing.IsCompleted);
        Assert.Equal(StreamingConnectionState.Connecting, harness.Transport.State);

        socket.PushBinary(Confirmation);

        await using var subscription = await subscribing;
        Assert.Equal(IbmFrame, await socket.NextSentAsync(ct));
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Holds_the_subscriptions_a_reconnect_re_sends_the_same_way()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        var first = harness.Socket;
        Assert.Equal(IbmFrame, await first.NextSentAsync(ct));

        // A reconnect walks into the same gap, and would lose every subscription it exists to keep.
        harness.Connector.Confirmation = null;
        first.Drop();

        var second = await harness.Connector.WaitForSocketAsync(1, ct);
        await Task.Delay(100, ct);
        Assert.Empty(second.Sent);

        second.PushBinary(Confirmation);

        Assert.Equal(IbmFrame, await second.NextSentAsync(ct));
    }

    [Fact]
    public async Task Holds_the_keep_alive_until_the_session_is_confirmed_too()
    {
        var ct = Within();
        await using var harness = new Harness(o =>
        {
            o.Streaming.KeepAliveInterval = Duration.FromMilliseconds(20);
            o.Streaming.SessionConfirmationTimeout = Duration.FromSeconds(30);
        });
        harness.Connector.Confirmation = null;

        var connecting = harness.Transport.ConnectAsync(ct);
        var socket = await harness.Connector.WaitForSocketAsync(0, ct);

        // Several keep-alive intervals pass with nothing on the socket: 'tic' is not exempt, because
        // a keep-alive for a session that does not exist keeps nothing alive.
        await Task.Delay(100, ct);
        Assert.Empty(socket.Sent);

        socket.PushBinary(Confirmation);
        await connecting;

        Assert.Equal("tic", await socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task Fails_the_open_when_ibkr_never_confirms_the_session()
    {
        var ct = Within();
        await using var harness = new Harness(
            o => o.Streaming.SessionConfirmationTimeout = Duration.FromMilliseconds(50));
        harness.Connector.Confirmation = null;

        var ex = await Assert.ThrowsAsync<IbkrStreamingException>(() => harness.Transport.ConnectAsync(ct));

        Assert.Contains("did not confirm the session", ex.Message, StringComparison.Ordinal);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);

        // The socket it opened is not left behind, and nothing went out on it.
        var socket = Assert.Single(harness.Connector.Sockets);
        Assert.True(socket.Disposed);
        Assert.Empty(socket.Sent);

        // A failed open is not a lost connection, so nothing is reconnecting on it either.
        await Task.Delay(100, ct);
        Assert.Single(harness.Connector.Sockets);
    }

    [Fact]
    public async Task Sends_as_soon_as_the_socket_opens_when_the_wait_is_turned_off()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.SessionConfirmationTimeout = Duration.Zero);
        harness.Connector.Confirmation = null;

        // The escape hatch for a mechanism that confirms differently, or not at all.
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);

        Assert.Equal(IbmFrame, await harness.Socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task Disposing_a_subscription_sends_the_unsubscribe_frame_and_ends_the_enumeration()
    {
        var ct = Within();
        await using var harness = new Harness();
        var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);

        await subscription.DisposeAsync();

        Assert.Equal("umd+8314+{}", await harness.Socket.NextSentAsync(ct));
        Assert.True(subscription.IsCompleted);
        Assert.Empty(await subscription.ReadAllAsync(ct).ToListAsync(ct));

        // Messages for a closed topic go nowhere, and the socket stays open for everyone else.
        harness.Socket.Push("""{"topic":"smd+8314","31":"1"}""");
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Disposing_a_subscription_whose_unsubscribe_is_waiting_when_the_socket_drops_does_not_throw()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.Reconnect = false);
        var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);

        // Another subscribe holds the send gate: its frame reached the socket, and the socket never
        // completes the send.
        harness.Socket.HoldSends();
        var opening = harness.Transport.SubscribeAsync(AppleTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);

        // The unsubscribe frame queues behind it, and then the line drops.
        var disposing = subscription.DisposeAsync().AsTask();
        harness.Socket.Drop();

        await disposing;
        await Assert.ThrowsAsync<IbkrStreamingException>(() => opening);

        // The lost line was reported the way one lost mid-send is, and the frame never went out.
        var failure = Assert.Single(harness.Logger.Entries, e => e.EventId.Name == "UnsubscribeFailed");
        Assert.IsType<IbkrStreamingException>(failure.Exception);
        Assert.DoesNotContain("umd+8314+{}", harness.Socket.Sent);
        Assert.True(subscription.IsCompleted);
    }

    [Fact]
    public async Task Reassembles_a_message_the_socket_delivers_in_pieces()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);

        // Well past the 16 KiB the read loop asks for at a time.
        var text = new string('x', 50_000);
        harness.Socket.Push($$"""{"topic":"smd+8314","58":"{{text}}"}""");

        var message = await subscription.ReadAsync(ct);

        Assert.Equal(text, message.Body.GetProperty("58").GetString());
    }

    [Fact]
    public async Task Listening_for_an_unsolicited_topic_opens_nothing_until_asked()
    {
        var ct = Within();
        await using var harness = new Harness();

        await using var status = await harness.Transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited(StreamingTopics.AuthenticationStatus), ct);

        Assert.Empty(harness.Connector.Requests);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);

        await harness.Transport.ConnectAsync(ct);
        harness.Socket.Push(Fixture.ReadText("Responses/websocket/authentication-status.json"));

        var message = await status.ReadAsync(ct);

        Assert.Equal("sts", message.Topic);
        Assert.Empty(harness.Socket.Sent);
    }

    [Fact]
    public async Task Tracks_heartbeats_and_authentication_status_whether_or_not_anybody_listens()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var marker = await harness.Transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited("marker"), ct);
        await harness.Transport.ConnectAsync(ct);

        Assert.Null(harness.Transport.LastHeartbeatAt);
        Assert.Null(harness.Transport.IsBrokerageSessionAuthenticated);

        harness.Socket.Push(Fixture.ReadText("Responses/websocket/system-connection.json"));
        harness.Socket.Push("""{"topic":"system","hb":1712596911593}""");
        harness.Socket.Push("""{"topic":"sts","args":{"authenticated":false,"competing":true}}""");
        harness.Socket.Push("""{"topic":"marker"}""");

        // Dispatch is sequential, so the marker arriving means the three before it were handled.
        await marker.ReadAsync(ct);

        Assert.Equal(Start, harness.Transport.LastHeartbeatAt);
        Assert.False(harness.Transport.IsBrokerageSessionAuthenticated);
    }

    [Fact]
    public async Task Ignores_what_it_cannot_read_and_keeps_going()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);

        harness.Socket.Push("not json");
        harness.Socket.Push("""{"no":"topic"}""");
        harness.Socket.Push("[1,2,3]");
        harness.Socket.Push("""{"topic":"smd+8314","31":"1"}""");

        var message = await subscription.ReadAsync(ct);

        Assert.Equal("1", message.Body.GetProperty("31").GetString());
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Keeps_the_socket_alive_with_tic()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.KeepAliveInterval = Duration.FromMilliseconds(20));

        await harness.Transport.ConnectAsync(ct);

        Assert.Equal("tic", await harness.Socket.NextSentAsync(ct));
        Assert.Equal("tic", await harness.Socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task Sends_a_raw_frame_only_while_the_socket_is_open()
    {
        var ct = Within();
        await using var harness = new Harness();

        var closed = await Assert.ThrowsAsync<IbkrStreamingException>(() => harness.Transport.SendAsync("tic", ct));
        Assert.Contains("not open", closed.Message, StringComparison.Ordinal);

        await harness.Transport.ConnectAsync(ct);
        await harness.Transport.SendAsync("tic", ct);

        Assert.Equal("tic", await harness.Socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task A_send_waiting_its_turn_when_the_socket_drops_fails_the_same_way_as_one_mid_send()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.Reconnect = false);
        await harness.Transport.ConnectAsync(ct);

        harness.Socket.HoldSends();
        var midSend = harness.Transport.SendAsync("tic", ct);
        await harness.Socket.NextSentAsync(ct);
        var waiting = harness.Transport.SendAsync("tic", ct);

        harness.Socket.Drop();

        var lost = await Assert.ThrowsAsync<IbkrStreamingException>(() => waiting);
        Assert.Contains("closed", lost.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<IbkrStreamingException>(() => midSend);
        Assert.Equal(["tic"], harness.Socket.Sent);
    }

    [Fact]
    public async Task A_send_waiting_its_turn_still_honours_the_callers_cancellation()
    {
        var ct = Within();
        await using var harness = new Harness();
        await harness.Transport.ConnectAsync(ct);

        harness.Socket.HoldSends();
        var midSend = harness.Transport.SendAsync("tic", ct);
        await harness.Socket.NextSentAsync(ct);
        using var caller = new CancellationTokenSource();
        var waiting = harness.Transport.SendAsync("tic", caller.Token);

        caller.Cancel();

        // The caller's own cancellation is theirs to see; only the connection's is a lost line.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        // The line is fine: the send in progress completes once the socket lets it.
        harness.Socket.ReleaseSends();
        await midSend;
        Assert.Equal(["tic"], harness.Socket.Sent);
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Reconnects_and_resubscribes_when_the_socket_drops()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        var first = harness.Socket;
        await first.NextSentAsync(ct);

        first.Drop();

        var second = await harness.Connector.WaitForSocketAsync(1, ct);
        Assert.Equal(IbmFrame, await second.NextSentAsync(ct));
        Assert.True(first.Disposed);
        Assert.Equal(2, harness.Session.EnsureCalls);

        // The consumer's enumeration survived the gap.
        second.Push("""{"topic":"smd+8314","31":"2"}""");
        var message = await subscription.ReadAsync(ct);
        Assert.Equal("2", message.Body.GetProperty("31").GetString());
        Assert.Equal(StreamingConnectionState.Connected, harness.Transport.State);
    }

    [Fact]
    public async Task Keeps_trying_while_the_gateway_stays_unreachable()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);
        harness.Connector.FailNextConnect(new WebSocketException(WebSocketError.Faulted, "still down"));
        harness.Connector.FailNextConnect(new HttpRequestException("still down"));

        harness.Socket.Drop();

        var recovered = await harness.Connector.WaitForSocketAsync(1, ct);
        Assert.Equal(IbmFrame, await recovered.NextSentAsync(ct));
        Assert.Equal(4, harness.Connector.Requests.Count);
    }

    [Fact]
    public async Task Reconnects_when_the_server_closes_the_socket()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);

        harness.Socket.PushClose();

        var second = await harness.Connector.WaitForSocketAsync(1, ct);
        Assert.Equal(IbmFrame, await second.NextSentAsync(ct));
    }

    [Fact]
    public async Task Completes_every_subscription_with_the_failure_when_reconnect_is_off()
    {
        var ct = Within();
        await using var harness = new Harness(o => o.Streaming.Reconnect = false);
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await harness.Socket.NextSentAsync(ct);

        harness.Socket.Drop();

        var ex = await Assert.ThrowsAsync<IbkrStreamingException>(async () =>
        {
            await foreach (var _ in subscription.ReadAllAsync(ct))
            {
            }
        });

        Assert.Contains("reconnection is disabled", ex.Message, StringComparison.Ordinal);
        Assert.IsType<WebSocketException>(ex.InnerException);
        Assert.Single(harness.Connector.Sockets);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);
    }

    [Fact]
    public async Task Hands_a_slow_consumer_the_latest_message_and_counts_the_rest()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        await using var marker = await harness.Transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited("marker"), ct);

        harness.Socket.Push("""{"topic":"smd+8314","31":"1"}""");
        harness.Socket.Push("""{"topic":"smd+8314","31":"2"}""");
        harness.Socket.Push("""{"topic":"smd+8314","31":"3"}""");
        harness.Socket.Push("""{"topic":"marker"}""");
        await marker.ReadAsync(ct);

        var message = await subscription.ReadAsync(ct);

        Assert.Equal("3", message.Body.GetProperty("31").GetString());
        Assert.Equal(2, subscription.DroppedMessages);
    }

    [Fact]
    public async Task A_subscription_can_ask_for_a_deeper_buffer()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(
            IbmTopOfBook with { BufferCapacity = 3 }, ct);
        await using var marker = await harness.Transport.SubscribeAsync(
            StreamingSubscriptionRequest.Unsolicited("marker"), ct);

        harness.Socket.Push("""{"topic":"smd+8314","31":"1"}""");
        harness.Socket.Push("""{"topic":"smd+8314","31":"2"}""");
        harness.Socket.Push("""{"topic":"smd+8314","31":"3"}""");
        harness.Socket.Push("""{"topic":"marker"}""");
        await marker.ReadAsync(ct);

        Assert.Equal("1", (await subscription.ReadAsync(ct)).Body.GetProperty("31").GetString());
        Assert.Equal("2", (await subscription.ReadAsync(ct)).Body.GetProperty("31").GetString());
        Assert.Equal("3", (await subscription.ReadAsync(ct)).Body.GetProperty("31").GetString());
        Assert.Equal(0, subscription.DroppedMessages);
    }

    [Fact]
    public async Task Closing_sends_the_close_frame_and_completes_every_subscription()
    {
        var ct = Within();
        await using var harness = new Harness();
        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        var socket = harness.Socket;

        await harness.Transport.CloseAsync(ct);

        Assert.Equal(WebSocketState.Closed, socket.State);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseStatus);
        Assert.True(socket.Disposed);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);
        Assert.True(subscription.IsCompleted);
        Assert.Empty(await subscription.ReadAllAsync(ct).ToListAsync(ct));

        // Closed deliberately, so nothing tries to reopen it.
        await Task.Delay(100, ct);
        Assert.Single(harness.Connector.Sockets);
    }

    [Fact]
    public async Task Can_be_opened_again_after_closing()
    {
        var ct = Within();
        await using var harness = new Harness();
        await harness.Transport.ConnectAsync(ct);
        await harness.Transport.CloseAsync(ct);

        await using var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);

        Assert.Equal(2, harness.Connector.Sockets.Count);
        Assert.Equal(IbmFrame, await harness.Socket.NextSentAsync(ct));
    }

    [Fact]
    public async Task Disposing_closes_the_socket_and_refuses_further_use()
    {
        var ct = Within();
        var harness = new Harness();
        await harness.Transport.ConnectAsync(ct);
        var socket = harness.Socket;

        await harness.Transport.DisposeAsync();
        await harness.Transport.DisposeAsync();

        Assert.True(socket.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => harness.Transport.ConnectAsync(ct));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => harness.Transport.SubscribeAsync(IbmTopOfBook, ct));
    }

    [Fact]
    public async Task Disposing_synchronously_closes_the_socket_too()
    {
        var ct = Within();
        var harness = new Harness();
        var subscription = await harness.Transport.SubscribeAsync(IbmTopOfBook, ct);
        var socket = harness.Socket;

        harness.Transport.Dispose();
        harness.Transport.Dispose();

        Assert.True(socket.Disposed);
        Assert.Equal(StreamingConnectionState.Disconnected, harness.Transport.State);
        Assert.False(await subscription.ReadAllAsync(ct).AnyAsync(ct));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => harness.Transport.ConnectAsync(ct));
    }

    [Fact]
    public async Task A_subscription_that_cannot_be_opened_is_not_kept()
    {
        var ct = Within();
        await using var harness = new Harness();
        harness.Session.Ready = false;

        await Assert.ThrowsAsync<IbkrAuthenticationException>(() => harness.Transport.SubscribeAsync(IbmTopOfBook, ct));

        harness.Session.Ready = true;
        await harness.Transport.ConnectAsync(ct);

        // Nothing is replayed for a subscription that never opened.
        await harness.Transport.SendAsync("tic", ct);
        Assert.Equal(["tic"], harness.Socket.Sent);
    }
}
