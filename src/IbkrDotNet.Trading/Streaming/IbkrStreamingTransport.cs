using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// The default <see cref="IIbkrStreamingTransport"/>, over a <see cref="WebSocket"/> opened by an
/// <see cref="IIbkrWebSocketConnector"/>.
/// </summary>
/// <remarks>
/// <para>
/// One read loop serves every subscription. It parses each message, routes it by its <c>topic</c>
/// field, and never waits on a consumer: each subscription has a bounded buffer, and a consumer that
/// falls behind loses messages by the configured <see cref="IbkrStreamingOptions.Overflow"/> rule
/// rather than holding everyone else up.
/// </para>
/// <para>
/// A socket that drops is reopened in the background with the same credentials, and every live
/// subscription is re-sent, so a consumer sees a gap rather than an end. The state of a
/// subscription therefore lives here, not in the topic clients: the transport is what knows which
/// frames to replay.
/// </para>
/// <para>
/// Register it as a singleton, as <c>AddIbkrTrading</c> in <c>IbkrDotNet.Extensions.DependencyInjection</c>
/// does. It holds the one socket the session gets, and every topic client shares it.
/// </para>
/// </remarks>
public sealed class IbkrStreamingTransport : IIbkrStreamingTransport, IDisposable
{
    private const int ReceiveChunkSize = 16 * 1024;
    private const string SessionCookiePrefix = "api=";

    private readonly IIbkrSessionManager _sessionManager;
    private readonly IbkrSessionState _sessionState;
    private readonly IIbkrAuthenticator _authenticator;
    private readonly IIbkrWebSocketConnector _connector;
    private readonly IbkrTradingOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<IbkrStreamingTransport> _logger;

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly List<StreamingSubscription> _subscriptions = [];
    private readonly CancellationTokenSource _lifetime = new();

    private volatile StreamingSubscription[] _routes = [];
    private Connection? _connection;
    private CancellationTokenSource? _reconnectCts;
    private int _state;
    private int _reconnecting;
    private volatile bool _closeRequested;
    private volatile bool _disposed;
    private Instant? _lastHeartbeatAt;
    private bool? _authenticated;

    /// <summary>Creates the transport over <see cref="ClientWebSocketConnector"/>.</summary>
    /// <param name="sessionManager">Establishes the brokerage session the socket needs.</param>
    /// <param name="sessionState">The session token the upgrade request presents as a cookie.</param>
    /// <param name="authenticator">The authentication mechanism, for the credential the upgrade request carries.</param>
    /// <param name="options">The client options.</param>
    /// <param name="clock">The clock messages are timestamped with.</param>
    /// <param name="logger">The logger.</param>
    public IbkrStreamingTransport(
        IIbkrSessionManager sessionManager,
        IbkrSessionState sessionState,
        IIbkrAuthenticator authenticator,
        IOptions<IbkrTradingOptions> options,
        IClock clock,
        ILogger<IbkrStreamingTransport> logger)
        : this(sessionManager, sessionState, authenticator, ClientWebSocketConnector.Instance, options, clock, logger)
    {
    }

    /// <summary>Creates the transport over a connector of the caller's choosing.</summary>
    /// <param name="sessionManager">Establishes the brokerage session the socket needs.</param>
    /// <param name="sessionState">The session token the upgrade request presents as a cookie.</param>
    /// <param name="authenticator">The authentication mechanism, for the credential the upgrade request carries.</param>
    /// <param name="connector">Opens the socket.</param>
    /// <param name="options">The client options.</param>
    /// <param name="clock">The clock messages are timestamped with.</param>
    /// <param name="logger">The logger.</param>
    public IbkrStreamingTransport(
        IIbkrSessionManager sessionManager,
        IbkrSessionState sessionState,
        IIbkrAuthenticator authenticator,
        IIbkrWebSocketConnector connector,
        IOptions<IbkrTradingOptions> options,
        IClock clock,
        ILogger<IbkrStreamingTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(sessionState);
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionManager = sessionManager;
        _sessionState = sessionState;
        _authenticator = authenticator;
        _connector = connector;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
        _options.Streaming.Validate();
    }

    /// <inheritdoc />
    public StreamingConnectionState State => (StreamingConnectionState)Volatile.Read(ref _state);

    /// <inheritdoc />
    public Instant? LastHeartbeatAt
    {
        get
        {
            lock (_sync)
            {
                return _lastHeartbeatAt;
            }
        }
    }

    /// <inheritdoc />
    public bool? IsBrokerageSessionAuthenticated
    {
        get
        {
            lock (_sync)
            {
                return _authenticated;
            }
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _connection) is not null)
            {
                return;
            }

            _closeRequested = false;
            await OpenAsync(StreamingConnectionState.Disconnected, cancellationToken).ConfigureAwait(false);
            await ResubscribeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _closeRequested = true;
        CancelReconnect();

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Taken out of the field first so the read loop, which ends when the server answers the
            // close frame, sees a connection that is no longer current and leaves it to this method.
            var connection = Interlocked.Exchange(ref _connection, null);
            if (connection is not null)
            {
                await ShutdownAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            SetState(StreamingConnectionState.Disconnected);
            CompleteAll(error: null);
            _connectGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<StreamingSubscription> SubscribeAsync(
        StreamingSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        var subscription = CreateSubscription(request);

        if (request.SubscribeFrame is not { } frame)
        {
            // Listening for an unsolicited topic has no side effect on IBKR, so it opens nothing.
            Add(subscription);
            return subscription;
        }

        // Under the connect gate so that a reconnect cannot interleave: either this frame goes out
        // on the socket that is open now, or the socket is opened here and every registered
        // subscription, this one included, is sent exactly once.
        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Add(subscription);
            if (Volatile.Read(ref _connection) is null)
            {
                _closeRequested = false;
                await OpenAsync(StreamingConnectionState.Disconnected, cancellationToken).ConfigureAwait(false);
                await ResubscribeAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            Remove(subscription);
            subscription.Complete(error: null);
            throw;
        }
        finally
        {
            _connectGate.Release();
        }

        return subscription;
    }

    /// <inheritdoc />
    public async Task SendAsync(string frame, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(frame);

        var connection = Volatile.Read(ref _connection);
        if (connection is null)
        {
            throw new IbkrStreamingException(
                $"The WebSocket is not open (state {State}), so the '{TopicOf(frame)}' frame was not sent.");
        }

        await SendCoreAsync(connection, frame, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IbkrStreamingException or WebSocketException or ObjectDisposedException)
        {
            StreamingLog.CloseAbandoned(_logger, ex);
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);
        _lifetime.Dispose();
        _connectGate.Dispose();
        _sendGate.Dispose();
    }

    /// <summary>
    /// Closes the socket and releases the transport, blocking until the close handshake completes or
    /// <see cref="IbkrStreamingOptions.CloseTimeout"/> elapses.
    /// </summary>
    /// <remarks>
    /// <see cref="DisposeAsync"/> is the one to call. This exists for a container that is disposed
    /// synchronously, which would otherwise refuse to dispose a service that only supports
    /// <see cref="IAsyncDisposable"/>; a host disposes asynchronously and never comes here.
    /// </remarks>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task OpenAsync(StreamingConnectionState onFailure, CancellationToken cancellationToken)
    {
        SetState(StreamingConnectionState.Connecting);
        try
        {
            // The socket needs the same brokerage session as /iserver, and the tickle inside this
            // call is what yields the session token the upgrade request must present.
            var status = await _sessionManager.EnsureBrokerageSessionAsync(cancellationToken).ConfigureAwait(false);
            if (!status.IsReadyToTrade)
            {
                throw new IbkrAuthenticationException(
                    "The WebSocket needs an established brokerage session and there is none " +
                    $"(connected={status.Connected}, authenticated={status.Authenticated}, " +
                    $"established={status.Established}, competing={status.Competing}). " +
                    (status.Message ?? status.Fail ?? "Log in and try again."));
            }

            var token = _sessionState.SessionToken;
            if (string.IsNullOrEmpty(token))
            {
                throw new IbkrAuthenticationException(
                    "IBKR's /tickle response carried no session token, and the WebSocket upgrade " +
                    "request must present one as the api= cookie.");
            }

            var credential = await _authenticator.GetStreamingCredentialAsync(cancellationToken).ConfigureAwait(false);
            var address = BuildAddress(credential);
            var visible = address.GetLeftPart(UriPartial.Path);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Cookie"] = SessionCookiePrefix + token,
                ["User-Agent"] = _options.UserAgent,
                ["Origin"] = _options.Streaming.Origin ?? _options.ResolveBaseAddress().GetLeftPart(UriPartial.Authority),
            };

            StreamingLog.Opening(_logger, visible);

            WebSocket socket;
            try
            {
                socket = await _connector
                    .ConnectAsync(new StreamingConnectRequest(address, headers, _options.Streaming.ConfigureClientWebSocket), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException
                                        or System.Security.Authentication.AuthenticationException)
            {
                throw new IbkrStreamingException($"The WebSocket at {visible} could not be opened: {ex.Message}", ex);
            }

            var connection = new Connection(socket);
            Volatile.Write(ref _connection, connection);
            SetState(StreamingConnectionState.Connected);

            connection.ReadLoop = RunReadLoopAsync(connection);
            connection.KeepAlive = RunKeepAliveAsync(connection);

            StreamingLog.Opened(_logger, visible);
        }
        catch
        {
            SetState(onFailure);
            throw;
        }
    }

    private Uri BuildAddress(StreamingCredential? credential)
    {
        var address = _options.ResolveStreamingAddress();
        if (credential is null)
        {
            return address;
        }

        var parameter = $"{Uri.EscapeDataString(credential.ParameterName)}={Uri.EscapeDataString(credential.Value)}";
        var existing = address.Query;
        var separator = string.IsNullOrEmpty(existing) ? "?" : "&";

        return new Uri(address.GetLeftPart(UriPartial.Path) + existing + separator + parameter);
    }

    private async Task ResubscribeAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _routes)
        {
            if (subscription.Request.SubscribeFrame is { } frame)
            {
                await SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendCoreAsync(Connection connection, string frame, CancellationToken cancellationToken)
    {
        var topic = TopicOf(frame);
        var payload = Encoding.UTF8.GetBytes(frame);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connection.Cts.Token);

        // The wait for the gate sits inside the try with the send: a connection lost while a frame
        // waits its turn is the same lost line as one that fails mid-send, and surfaces the same way.
        var acquired = false;
        try
        {
            // A WebSocket permits one send at a time.
            await _sendGate.WaitAsync(linked.Token).ConfigureAwait(false);
            acquired = true;

            // The gate can be won in the same instant the connection is lost: the runtime grants a
            // wait whose token was already cancelled when the release lands first. Nothing goes out
            // on a line that is gone.
            linked.Token.ThrowIfCancellationRequested();

            StreamingLog.Sending(_logger, topic);
            await connection.Socket
                .SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, linked.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            throw new IbkrStreamingException($"The '{topic}' frame could not be sent: {ex.Message}", ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IbkrStreamingException($"The '{topic}' frame could not be sent: the WebSocket closed.");
        }
        finally
        {
            if (acquired)
            {
                _sendGate.Release();
            }
        }
    }

    private async Task RunReadLoopAsync(Connection connection)
    {
        var socket = connection.Socket;
        var token = connection.Cts.Token;
        var message = new ArrayBufferWriter<byte>(ReceiveChunkSize);
        Exception? failure = null;
        var closedByServer = false;

        try
        {
            while (!token.IsCancellationRequested)
            {
                message.ResetWrittenCount();
                ValueWebSocketReceiveResult result;
                do
                {
                    var memory = message.GetMemory(ReceiveChunkSize);
                    result = await socket.ReceiveAsync(memory, token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    message.Advance(result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    closedByServer = true;
                    break;
                }

                // Text or binary, dispatched the same: the payload is UTF-8 JSON either way, and a
                // Client Portal Gateway sends every frame as binary — session confirmations and
                // heartbeats included. Close is the only other member of the enum and has already
                // broken out above, so there is no third case to drop.
                Dispatch(message.WrittenMemory);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Closing or disposing; nothing to report.
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            failure = ex;
        }

        OnReadLoopEnded(connection, closedByServer, failure);
    }

    private void OnReadLoopEnded(Connection connection, bool closedByServer, Exception? failure)
    {
        // CloseAsync takes the connection out of the field before waiting for this loop to end,
        // so a connection that is no longer current is being shut down deliberately.
        if (!ReferenceEquals(Volatile.Read(ref _connection), connection))
        {
            return;
        }

        Interlocked.CompareExchange(ref _connection, null, connection);
        connection.Cts.Cancel();
        connection.Dispose();

        if (_disposed || _closeRequested)
        {
            SetState(StreamingConnectionState.Disconnected);
            return;
        }

        if (failure is null)
        {
            StreamingLog.ClosedByServer(_logger, closedByServer);
        }
        else
        {
            StreamingLog.ConnectionLost(_logger, failure);
        }

        if (!_options.Streaming.Reconnect)
        {
            SetState(StreamingConnectionState.Disconnected);
            CompleteAll(failure is null
                ? new IbkrStreamingException("The WebSocket connection was lost and reconnection is disabled.")
                : new IbkrStreamingException("The WebSocket connection was lost and reconnection is disabled.", failure));
            return;
        }

        SetState(StreamingConnectionState.Reconnecting);
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            Volatile.Write(ref _reconnectCts, cts);
            _ = Task.Run(() => ReconnectAsync(cts), CancellationToken.None);
        }
    }

    private async Task ReconnectAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        var delay = _options.Streaming.ReconnectDelay;
        var attempt = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                attempt++;
                await Task.Delay(delay.ToTimeSpan(), token).ConfigureAwait(false);

                await _connectGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (_closeRequested || _disposed || Volatile.Read(ref _connection) is not null)
                    {
                        return;
                    }

                    StreamingLog.Reconnecting(_logger, attempt);
                    await OpenAsync(StreamingConnectionState.Reconnecting, token).ConfigureAwait(false);
                    await ResubscribeAsync(token).ConfigureAwait(false);
                    StreamingLog.Reconnected(_logger, attempt);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Anything at all: a background loop that dies on an unexpected exception
                    // leaves the transport reconnecting forever with nobody trying.
                    StreamingLog.ReconnectFailed(_logger, attempt, delay, ex);
                    delay = Duration.Min(delay * 2, _options.Streaming.ReconnectMaxDelay);
                }
                finally
                {
                    _connectGate.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Closed or disposed while waiting.
        }
        finally
        {
            Volatile.Write(ref _reconnecting, 0);
            Interlocked.CompareExchange(ref _reconnectCts, null, cts);
            cts.Dispose();
        }
    }

    private async Task RunKeepAliveAsync(Connection connection)
    {
        var token = connection.Cts.Token;
        using var timer = new PeriodicTimer(_options.Streaming.KeepAliveInterval.ToTimeSpan());

        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await SendCoreAsync(connection, StreamingTopics.KeepAlive, token).ConfigureAwait(false);
                }
                catch (IbkrStreamingException ex)
                {
                    StreamingLog.KeepAliveFailed(_logger, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection is being torn down.
        }
    }

    private async Task ShutdownAsync(Connection connection, CancellationToken cancellationToken)
    {
        var socket = connection.Socket;
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(_options.Streaming.CloseTimeout.ToTimeSpan());
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

                await _sendGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    await socket
                        .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", linked.Token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _sendGate.Release();
                }

                // The read loop ends when the server answers with its own close frame.
                await connection.ReadLoop.WaitAsync(linked.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException
                                    or InvalidOperationException or OperationCanceledException)
        {
            StreamingLog.CloseAbandoned(_logger, ex);
            socket.Abort();
        }
        finally
        {
            connection.Cts.Cancel();
            await connection.KeepAlive.ConfigureAwait(false);
            await connection.ReadLoop.ConfigureAwait(false);
            connection.Dispose();
            StreamingLog.Closed(_logger);
        }
    }

    private void Dispatch(ReadOnlyMemory<byte> utf8)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8);
        }
        catch (JsonException ex)
        {
            StreamingLog.Unreadable(_logger, ex);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("topic", out var topicElement) ||
                topicElement.ValueKind != JsonValueKind.String)
            {
                StreamingLog.NoTopic(_logger);
                return;
            }

            var topic = topicElement.GetString()!;
            var message = new StreamingMessage(topic, root.Clone(), _clock.GetCurrentInstant());

            Observe(topic, message);

            var delivered = 0;
            foreach (var subscription in _routes)
            {
                if (subscription.Request.Matches(topic))
                {
                    subscription.Deliver(message);
                    delivered++;
                }
            }

            if (delivered == 0)
            {
                StreamingLog.Unrouted(_logger, topic);
            }
        }
    }

    /// <summary>
    /// Keeps the transport's own view of the session from the unsolicited topics, whether or not
    /// anybody has subscribed to them.
    /// </summary>
    private void Observe(string topic, StreamingMessage message)
    {
        switch (topic)
        {
            case StreamingTopics.System:
                if (message.Body.TryGetProperty("hb", out _))
                {
                    lock (_sync)
                    {
                        _lastHeartbeatAt = message.ReceivedAt;
                    }
                }
                else if (message.Body.TryGetProperty("success", out _))
                {
                    StreamingLog.SessionConfirmed(_logger);
                }

                break;

            case StreamingTopics.AuthenticationStatus:
                if (message.Body.TryGetProperty("args", out var args) &&
                    args.ValueKind == JsonValueKind.Object &&
                    args.TryGetProperty("authenticated", out var authenticated) &&
                    authenticated.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    var value = authenticated.GetBoolean();
                    lock (_sync)
                    {
                        _authenticated = value;
                    }

                    if (!value)
                    {
                        StreamingLog.SessionNotAuthenticated(_logger);
                    }
                }

                break;

            default:
                break;
        }
    }

    private StreamingSubscription CreateSubscription(StreamingSubscriptionRequest request)
    {
        var capacity = request.BufferCapacity ?? _options.Streaming.BufferCapacity;
        if (capacity < 1)
        {
            throw new ArgumentException(
                $"{nameof(StreamingSubscriptionRequest.BufferCapacity)} must be at least 1.",
                nameof(request));
        }

        StreamingSubscription? created = null;
        var channel = Channel.CreateBounded<StreamingMessage>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = _options.Streaming.Overflow == StreamingOverflowMode.DropNewest
                    ? BoundedChannelFullMode.DropNewest
                    : BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            },
            _ => created?.CountDrop());

        created = new StreamingSubscription(request, channel, UnsubscribeAsync);
        return created;
    }

    private async ValueTask UnsubscribeAsync(StreamingSubscription subscription)
    {
        Remove(subscription);

        if (subscription.Request.UnsubscribeFrame is not { } frame || _disposed || Volatile.Read(ref _connection) is null)
        {
            return;
        }

        try
        {
            await SendAsync(frame, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IbkrStreamingException or ObjectDisposedException)
        {
            // The socket is gone, and with it the subscription IBKR held.
            StreamingLog.UnsubscribeFailed(_logger, subscription.Request.Topic, ex);
        }
    }

    private void Add(StreamingSubscription subscription)
    {
        lock (_sync)
        {
            _subscriptions.Add(subscription);
            _routes = [.. _subscriptions];
        }
    }

    private void Remove(StreamingSubscription subscription)
    {
        lock (_sync)
        {
            if (_subscriptions.Remove(subscription))
            {
                _routes = [.. _subscriptions];
            }
        }
    }

    private void CompleteAll(Exception? error)
    {
        StreamingSubscription[] all;
        lock (_sync)
        {
            all = [.. _subscriptions];
            _subscriptions.Clear();
            _routes = [];
        }

        foreach (var subscription in all)
        {
            subscription.Complete(error);
        }
    }

    private void CancelReconnect()
    {
        try
        {
            Volatile.Read(ref _reconnectCts)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The reconnect loop finished and disposed its own token source in the same instant.
        }
    }

    private void SetState(StreamingConnectionState state) => Volatile.Write(ref _state, (int)state);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// The topic part of a frame, for messages and logs. The rest may name an account.
    /// </summary>
    private static string TopicOf(string frame)
    {
        var separator = frame.IndexOf(StreamingFrame.Separator, StringComparison.Ordinal);
        return separator < 0 ? frame : frame[..separator];
    }

    private sealed class Connection(WebSocket socket) : IDisposable
    {
        public WebSocket Socket { get; } = socket;

        public CancellationTokenSource Cts { get; } = new();

        public Task ReadLoop { get; set; } = Task.CompletedTask;

        public Task KeepAlive { get; set; } = Task.CompletedTask;

        public void Dispose()
        {
            Cts.Dispose();
            Socket.Dispose();
        }
    }
}

internal static partial class StreamingLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Debug, Message = "Opening the IBKR WebSocket at {Address}.")]
    public static partial void Opening(ILogger logger, string address);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, Message = "The IBKR WebSocket at {Address} is open.")]
    public static partial void Opened(ILogger logger, string address);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Information, Message = "IBKR confirmed the WebSocket session.")]
    public static partial void SessionConfirmed(ILogger logger);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Warning,
        Message = "IBKR reports on the WebSocket that the brokerage session is no longer authenticated.")]
    public static partial void SessionNotAuthenticated(ILogger logger);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Warning, Message = "The IBKR WebSocket connection was lost.")]
    public static partial void ConnectionLost(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Information,
        Message = "The IBKR WebSocket ended (close frame from the server: {CloseFrame}).")]
    public static partial void ClosedByServer(ILogger logger, bool closeFrame);

    [LoggerMessage(EventId = 1306, Level = LogLevel.Information, Message = "Reconnecting the IBKR WebSocket (attempt {Attempt}).")]
    public static partial void Reconnecting(ILogger logger, int attempt);

    [LoggerMessage(
        EventId = 1307,
        Level = LogLevel.Warning,
        Message = "Reconnecting the IBKR WebSocket failed on attempt {Attempt}; waiting {Delay} before the next.")]
    public static partial void ReconnectFailed(ILogger logger, int attempt, Duration delay, Exception exception);

    [LoggerMessage(
        EventId = 1308,
        Level = LogLevel.Information,
        Message = "The IBKR WebSocket reconnected on attempt {Attempt} and its subscriptions were re-sent.")]
    public static partial void Reconnected(ILogger logger, int attempt);

    [LoggerMessage(EventId = 1309, Level = LogLevel.Debug, Message = "Sending '{Topic}' on the IBKR WebSocket.")]
    public static partial void Sending(ILogger logger, string topic);

    [LoggerMessage(EventId = 1310, Level = LogLevel.Warning, Message = "The IBKR WebSocket keep-alive could not be sent.")]
    public static partial void KeepAliveFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1311, Level = LogLevel.Debug, Message = "A WebSocket message on topic '{Topic}' had no subscriber.")]
    public static partial void Unrouted(ILogger logger, string topic);

    [LoggerMessage(EventId = 1312, Level = LogLevel.Debug, Message = "A WebSocket message could not be read as JSON.")]
    public static partial void Unreadable(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1313, Level = LogLevel.Debug, Message = "A WebSocket message carried no topic.")]
    public static partial void NoTopic(ILogger logger);

    [LoggerMessage(EventId = 1314, Level = LogLevel.Debug, Message = "The unsubscribe frame for '{Topic}' could not be sent.")]
    public static partial void UnsubscribeFailed(ILogger logger, string topic, Exception exception);

    [LoggerMessage(EventId = 1315, Level = LogLevel.Debug, Message = "The WebSocket close handshake was abandoned.")]
    public static partial void CloseAbandoned(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1316, Level = LogLevel.Information, Message = "The IBKR WebSocket is closed.")]
    public static partial void Closed(ILogger logger);
}
