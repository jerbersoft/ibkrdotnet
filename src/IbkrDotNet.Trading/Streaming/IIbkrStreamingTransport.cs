using NodaTime;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// The WebSocket transport every streaming topic runs over: one socket per session, opened with
/// the brokerage session's credentials, kept alive, reopened when it drops, and demultiplexed into
/// per-topic subscriptions.
/// </summary>
/// <remarks>
/// <para>
/// Topic clients are built on this. It is public for the same reason <see cref="Http.IIbkrApiClient"/>
/// is: a caller can reach a topic this library has not modelled through
/// <see cref="SubscribeAsync"/> and <see cref="SendAsync"/> without losing authentication,
/// keep-alive and reconnection.
/// </para>
/// <para>
/// The socket needs the same brokerage session as the <c>/iserver</c> endpoints, so opening it
/// goes through <see cref="Session.IIbkrSessionManager.EnsureBrokerageSessionAsync"/> first. It
/// also needs the REST <c>/tickle</c> to keep running; the <c>tic</c> topic this transport sends
/// keeps the socket alive, not the session behind it.
/// </para>
/// </remarks>
public interface IIbkrStreamingTransport : IAsyncDisposable
{
    /// <summary>The transport's current state.</summary>
    StreamingConnectionState State { get; }

    /// <summary>
    /// When IBKR's last <c>system</c> heartbeat arrived, or <see langword="null"/> before the first.
    /// </summary>
    /// <remarks>IBKR sends one every ten seconds while the socket is healthy.</remarks>
    Instant? LastHeartbeatAt { get; }

    /// <summary>
    /// Whether the brokerage session is authenticated, as last reported on the <c>sts</c> topic, or
    /// <see langword="null"/> before IBKR has said.
    /// </summary>
    bool? IsBrokerageSessionAuthenticated { get; }

    /// <summary>
    /// Opens the socket if it is not already open: confirms the brokerage session, sends the
    /// upgrade request with the session cookie and the mechanism's credential, and re-sends every
    /// subscription still registered from before.
    /// </summary>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <exception cref="Http.IbkrAuthenticationException">The brokerage session is not established.</exception>
    /// <exception cref="IbkrStreamingException">The socket could not be opened.</exception>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the socket and completes every subscription. The transport can be opened again.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cuts the close handshake short. The socket is aborted rather than left half-closed, and the
    /// call still returns normally.
    /// </param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a topic. A solicited request opens the socket first if it is not open and sends its
    /// subscribe frame; an unsolicited one only registers to be routed to.
    /// </summary>
    /// <param name="request">The topic to open.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>The subscription. Dispose it to close the topic.</returns>
    /// <exception cref="Http.IbkrAuthenticationException">The brokerage session is not established.</exception>
    /// <exception cref="IbkrStreamingException">The socket could not be opened or refused the frame.</exception>
    Task<StreamingSubscription> SubscribeAsync(
        StreamingSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a frame as written, for topics this library has not modelled. The socket must be open.
    /// </summary>
    /// <param name="frame">The frame, for example <c>tic</c>. See <see cref="StreamingFrame"/>.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <exception cref="IbkrStreamingException">The socket is not open or refused the frame.</exception>
    Task SendAsync(string frame, CancellationToken cancellationToken = default);
}
