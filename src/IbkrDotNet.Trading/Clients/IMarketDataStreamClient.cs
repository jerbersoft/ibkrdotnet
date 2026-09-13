using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// Streaming market data over the WebSocket: live top-of-book quotes for one instrument at a time.
/// </summary>
/// <remarks>
/// The streaming counterpart of <see cref="IMarketDataClient"/>. Where the snapshot endpoint is
/// polled, a stream is read: the same field tags, the same values, pushed as IBKR updates them
/// rather than fetched on request.
/// </remarks>
public interface IMarketDataStreamClient
{
    /// <summary>
    /// Streams quotes for an instrument as IBKR sends them, until the enumeration is disposed or
    /// the token is cancelled.
    /// </summary>
    /// <param name="conId">The instrument. One per stream; combos and spreads are not supported.</param>
    /// <param name="fields">
    /// The tick identifiers to stream, the same as for <see cref="IMarketDataClient.GetSnapshotAsync"/>.
    /// See <see cref="MarketDataField"/>. <see langword="null"/> streams <see cref="MarketDataField.TopOfBook"/>.
    /// </param>
    /// <param name="exchange">The data source, or <see langword="null"/> for SMART.</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The updates, as they arrive.</returns>
    /// <remarks>
    /// <para>
    /// Nothing is sent until the enumeration starts. It then opens the socket if it is not open,
    /// sends <c>smd</c>, and yields each message for the instrument as a
    /// <see cref="MarketDataUpdate"/>. When it ends — on a <c>break</c>, on cancellation, or on an
    /// exception — <c>umd</c> is sent so the instrument's market data line is released. Read a
    /// stream for only as long as it is wanted, because each open stream holds one of the
    /// account's lines, 100 by default.
    /// </para>
    /// <para>
    /// IBKR ends a stream 15 minutes after it was requested. The request is re-sent every
    /// <see cref="Configuration.IbkrStreamingOptions.MarketDataRenewalInterval"/>, 10 minutes by
    /// default, so the enumeration outlives that limit without the reader doing anything.
    /// </para>
    /// <para>
    /// Updates arrive at most every 500 ms. A reader that falls behind is handed the latest message
    /// rather than a backlog; see <see cref="Configuration.IbkrStreamingOptions.BufferCapacity"/>.
    /// The stream does not end when the socket drops and comes back; the transport re-sends the
    /// request and the updates resume.
    /// </para>
    /// <para>
    /// The REST <see cref="IMarketDataClient.UnsubscribeAsync"/> and
    /// <see cref="IMarketDataClient.UnsubscribeAllAsync"/> close the same backend streams. A stream
    /// closed that way goes quiet until its next renewal requests it again.
    /// </para>
    /// <para>
    /// Failures to open the socket or send the request surface from the first read, not from this
    /// call, which only validates its arguments.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="fields"/> is empty or holds a blank tag, or <paramref name="exchange"/> is
    /// blank or cannot be framed.
    /// </exception>
    /// <exception cref="Http.IbkrAuthenticationException">
    /// The brokerage session is not established. Thrown from the first read.
    /// </exception>
    /// <exception cref="Streaming.IbkrStreamingException">
    /// The socket could not be opened, or was lost with reconnection turned off. Thrown from a read.
    /// </exception>
    /// <exception cref="Serialization.IbkrSerializationException">
    /// A message could not be read as a <see cref="MarketDataUpdate"/>. Thrown from a read.
    /// </exception>
    IAsyncEnumerable<MarketDataUpdate> SubscribeAsync(
        ConId conId,
        IReadOnlyList<string>? fields = null,
        string? exchange = null,
        CancellationToken cancellationToken = default);
}
