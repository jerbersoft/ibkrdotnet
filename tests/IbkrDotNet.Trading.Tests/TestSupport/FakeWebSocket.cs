using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// A scripted <see cref="WebSocket"/>: the test pushes the messages IBKR would send and reads back
/// the frames the transport sent, without a network.
/// </summary>
public sealed class FakeWebSocket : WebSocket
{
    private readonly Channel<Inbound> _inbound = Channel.CreateUnbounded<Inbound>();
    private readonly Channel<string> _sent = Channel.CreateUnbounded<string>();
    private readonly List<string> _sentLog = [];
    private readonly Lock _sync = new();
    private Inbound? _pending;
    private int _pendingOffset;
    private volatile WebSocketState _state = WebSocketState.Open;
    private WebSocketCloseStatus? _closeStatus;
    private TaskCompletionSource? _hold;

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;

    public override string? CloseStatusDescription => null;

    public override WebSocketState State => _state;

    public override string? SubProtocol => null;

    public bool Disposed { get; private set; }

    public bool Aborted { get; private set; }

    /// <summary>Every frame the transport sent, in order.</summary>
    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sync)
            {
                return [.. _sentLog];
            }
        }
    }

    /// <summary>Queues a text message for the transport to read.</summary>
    public void Push(string text) =>
        _inbound.Writer.TryWrite(new Inbound(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text));

    /// <summary>Has the server close the socket cleanly.</summary>
    public void PushClose() => _inbound.Writer.TryWrite(new Inbound([], WebSocketMessageType.Close));

    /// <summary>Drops the connection: the next receive fails the way a lost network does.</summary>
    public void Drop() =>
        _inbound.Writer.TryComplete(
            new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The connection dropped."));

    /// <summary>
    /// Holds every send open from here on, the way a stalled network would, until
    /// <see cref="ReleaseSends"/>. A held frame is still recorded as sent, so a test can wait for it
    /// with <see cref="NextSentAsync"/> and know the transport is inside the send.
    /// </summary>
    public void HoldSends() =>
        Volatile.Write(ref _hold, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>Lets the held sends complete and stops holding new ones.</summary>
    public void ReleaseSends() => Interlocked.Exchange(ref _hold, null)?.TrySetResult();

    /// <summary>Waits for the next frame the transport sends.</summary>
    public async Task<string> NextSentAsync(CancellationToken cancellationToken) =>
        await _sent.Reader.ReadAsync(cancellationToken);

    public override void Abort()
    {
        Aborted = true;
        _state = WebSocketState.Aborted;
        _inbound.Writer.TryComplete(new WebSocketException(WebSocketError.InvalidState, "Aborted."));
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _state = WebSocketState.Closed;
        _inbound.Writer.TryWrite(new Inbound([], WebSocketMessageType.Close));
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _state = WebSocketState.CloseSent;

        // The server answers a close frame with its own.
        _inbound.Writer.TryWrite(new Inbound([], WebSocketMessageType.Close));
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Disposed = true;
        _inbound.Writer.TryComplete();
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (_pending is null)
        {
            Inbound next;
            try
            {
                next = await _inbound.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException ex) when (ex.InnerException is WebSocketException inner)
            {
                throw inner;
            }
            catch (ChannelClosedException)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Disposed.");
            }

            if (next.Type == WebSocketMessageType.Close)
            {
                _state = _state == WebSocketState.CloseSent ? WebSocketState.Closed : WebSocketState.CloseReceived;
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null);
            }

            _pending = next;
            _pendingOffset = 0;
        }

        // Handed over in pieces no larger than the caller's buffer, the way a real socket does.
        var remaining = _pending.Data.Length - _pendingOffset;
        var count = Math.Min(remaining, buffer.Count);
        Array.Copy(_pending.Data, _pendingOffset, buffer.Array!, buffer.Offset, count);
        _pendingOffset += count;

        var endOfMessage = _pendingOffset == _pending.Data.Length;
        if (endOfMessage)
        {
            _pending = null;
        }

        return new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage);
    }

    public override async Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        if (_state != WebSocketState.Open)
        {
            throw new WebSocketException(WebSocketError.InvalidState, $"The socket is {_state}.");
        }

        var text = Encoding.UTF8.GetString(buffer);
        lock (_sync)
        {
            _sentLog.Add(text);
        }

        _sent.Writer.TryWrite(text);

        if (Volatile.Read(ref _hold) is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed record Inbound(byte[] Data, WebSocketMessageType Type);
}
