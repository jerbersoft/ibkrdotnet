using System.Net.WebSockets;
using NodaTime;

namespace IbkrDotNet.Trading.Configuration;

/// <summary>
/// Configures the WebSocket transport that streaming topics run over.
/// </summary>
/// <remarks>
/// Reached through <see cref="IbkrTradingOptions.Streaming"/>. Nothing here is required: the
/// defaults open <c>wss://{host}/v1/api/ws</c> under the configured base address, keep it alive,
/// reopen it when it drops, and hand each subscriber only the latest message when it falls behind.
/// </remarks>
public sealed class IbkrStreamingOptions
{
    /// <summary>
    /// An explicit socket address, overriding the one derived from the base address.
    /// </summary>
    /// <remarks>
    /// By default the socket is <c>/v1/api/ws</c> under <see cref="IbkrTradingOptions.ResolveBaseAddress"/>
    /// with the scheme swapped to <c>wss</c>, so a gateway at <c>https://localhost:5050</c> is
    /// reached at <c>wss://localhost:5050/v1/api/ws</c>. Set this only when the socket lives
    /// somewhere else.
    /// </remarks>
    public Uri? Address { get; set; }

    /// <summary>
    /// The <c>Origin</c> header sent with the upgrade request. Defaults to the base address.
    /// </summary>
    /// <remarks>
    /// IBKR's own examples send one and a bare <c>ClientWebSocket</c> does not, so the transport
    /// supplies the base address unless told otherwise.
    /// </remarks>
    public string? Origin { get; set; }

    /// <summary>
    /// How often the <c>tic</c> topic is sent to keep the socket session alive. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// IBKR asks for at least one per minute. This is in addition to, not instead of, the REST
    /// <c>/tickle</c> the brokerage session still needs; see
    /// <see cref="Session.IIbkrSessionManager.KeepAliveAsync"/>.
    /// </remarks>
    public Duration KeepAliveInterval { get; set; } = Duration.FromSeconds(30);

    /// <summary>
    /// Whether a socket that drops is reopened and its subscriptions re-sent. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Subscribers see nothing but a gap: their enumerations stay open across the reconnect. With
    /// this off, a dropped socket completes every subscription with the failure instead.
    /// </remarks>
    public bool Reconnect { get; set; } = true;

    /// <summary>
    /// The wait before the first reconnect attempt, doubled after each failed one up to
    /// <see cref="ReconnectMaxDelay"/>. Defaults to 1 second.
    /// </summary>
    public Duration ReconnectDelay { get; set; } = Duration.FromSeconds(1);

    /// <summary>The longest wait between reconnect attempts. Defaults to 30 seconds.</summary>
    public Duration ReconnectMaxDelay { get; set; } = Duration.FromSeconds(30);

    /// <summary>
    /// How many messages a subscription holds for a consumer that has not read them yet. Defaults to 1.
    /// </summary>
    /// <remarks>
    /// A slow consumer must never stall the read loop, because every other subscription shares it.
    /// With the default of one and <see cref="Overflow"/> at <see cref="StreamingOverflowMode.DropOldest"/>,
    /// a consumer that falls behind simply gets the latest message next, which is the right thing
    /// for a quote. A subscription can ask for more through
    /// <see cref="Streaming.StreamingSubscriptionRequest.BufferCapacity"/>.
    /// </remarks>
    public int BufferCapacity { get; set; } = 1;

    /// <summary>
    /// What happens to a message when a subscription's buffer is full. Defaults to
    /// <see cref="StreamingOverflowMode.DropOldest"/>.
    /// </summary>
    public StreamingOverflowMode Overflow { get; set; } = StreamingOverflowMode.DropOldest;

    /// <summary>
    /// How long the close handshake is given before the socket is abandoned. Defaults to 5 seconds.
    /// </summary>
    public Duration CloseTimeout { get; set; } = Duration.FromSeconds(5);

    /// <summary>
    /// A hook over the <see cref="ClientWebSocketOptions"/> before the upgrade request is sent.
    /// </summary>
    /// <remarks>
    /// This is where a caller trusts the Client Portal Gateway's self-signed certificate, through
    /// <see cref="ClientWebSocketOptions.RemoteCertificateValidationCallback"/>, or sets a proxy.
    /// It runs after the transport has set its own headers.
    /// </remarks>
    public Action<ClientWebSocketOptions>? ConfigureClientWebSocket { get; set; }

    /// <summary>Throws when the options are not usable.</summary>
    /// <exception cref="InvalidOperationException">A value is out of range.</exception>
    public void Validate()
    {
        if (KeepAliveInterval <= Duration.Zero)
        {
            throw new InvalidOperationException(
                $"{nameof(IbkrStreamingOptions)}.{nameof(KeepAliveInterval)} must be positive.");
        }

        if (ReconnectDelay <= Duration.Zero)
        {
            throw new InvalidOperationException(
                $"{nameof(IbkrStreamingOptions)}.{nameof(ReconnectDelay)} must be positive.");
        }

        if (ReconnectMaxDelay < ReconnectDelay)
        {
            throw new InvalidOperationException(
                $"{nameof(IbkrStreamingOptions)}.{nameof(ReconnectMaxDelay)} cannot be shorter than " +
                $"{nameof(ReconnectDelay)}.");
        }

        if (BufferCapacity < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(IbkrStreamingOptions)}.{nameof(BufferCapacity)} must be at least 1.");
        }

        if (CloseTimeout <= Duration.Zero)
        {
            throw new InvalidOperationException(
                $"{nameof(IbkrStreamingOptions)}.{nameof(CloseTimeout)} must be positive.");
        }
    }
}

/// <summary>
/// What a subscription does with a new message when its buffer is already full.
/// </summary>
/// <remarks>
/// There is deliberately no option to wait. Every subscription is fed by the one read loop, so a
/// consumer that could block it would stall every other subscription on the socket.
/// </remarks>
public enum StreamingOverflowMode
{
    /// <summary>Discard the oldest buffered message to make room. The consumer always sees the latest.</summary>
    DropOldest,

    /// <summary>Discard the message that just arrived. The consumer sees the earliest unread ones.</summary>
    DropNewest,
}
