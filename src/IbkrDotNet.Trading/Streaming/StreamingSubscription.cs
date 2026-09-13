using System.Threading.Channels;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// An open topic on the WebSocket: the messages routed to it, and the means of closing it.
/// </summary>
/// <remarks>
/// <para>
/// Read it with <see cref="ReadAllAsync"/>. The enumeration ends when the subscription is disposed
/// or the transport is closed, and throws when the socket is lost with reconnection turned off. It
/// does not end when the socket drops and comes back; the transport re-sends the request and the
/// messages resume.
/// </para>
/// <para>
/// Disposing sends the unsubscribe frame, where the request has one. Do it in a <c>finally</c>: a
/// market data stream left open keeps consuming one of the account's market data lines.
/// </para>
/// </remarks>
public sealed class StreamingSubscription : IAsyncDisposable
{
    private readonly Channel<StreamingMessage> _channel;
    private readonly Func<StreamingSubscription, ValueTask> _onDispose;
    private long _dropped;
    private int _disposed;

    internal StreamingSubscription(
        StreamingSubscriptionRequest request,
        Channel<StreamingMessage> channel,
        Func<StreamingSubscription, ValueTask> onDispose)
    {
        Request = request;
        _channel = channel;
        _onDispose = onDispose;
    }

    /// <summary>The request that opened the topic.</summary>
    public StreamingSubscriptionRequest Request { get; }

    /// <summary>
    /// How many messages were discarded because the consumer had not read the buffered ones.
    /// </summary>
    /// <remarks>
    /// Not an error for a quote feed, where the latest value is the one that matters. A count that
    /// keeps climbing on a topic where every message counts is the signal to raise
    /// <see cref="StreamingSubscriptionRequest.BufferCapacity"/>.
    /// </remarks>
    public long DroppedMessages => Interlocked.Read(ref _dropped);

    /// <summary>Whether the subscription has been closed and will deliver nothing further.</summary>
    public bool IsCompleted => _channel.Reader.Completion.IsCompleted;

    /// <summary>The messages routed to this subscription, as they arrive.</summary>
    /// <param name="cancellationToken">Ends the enumeration.</param>
    /// <exception cref="IbkrStreamingException">The socket was lost and reconnection is off.</exception>
    public IAsyncEnumerable<StreamingMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Waits for the next message.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="ChannelClosedException">The subscription has completed.</exception>
    /// <exception cref="IbkrStreamingException">The socket was lost and reconnection is off.</exception>
    public ValueTask<StreamingMessage> ReadAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAsync(cancellationToken);

    /// <summary>
    /// Closes the topic, sending its unsubscribe frame when it has one, and ends the enumeration.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            await _onDispose(this).ConfigureAwait(false);
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    internal void Deliver(StreamingMessage message) => _channel.Writer.TryWrite(message);

    internal void Complete(Exception? error) => _channel.Writer.TryComplete(error);

    internal void CountDrop() => Interlocked.Increment(ref _dropped);
}
