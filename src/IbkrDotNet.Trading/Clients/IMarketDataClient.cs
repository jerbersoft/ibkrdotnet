using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Clients;

/// <summary>The Trading Market Data endpoints: live snapshots and historical bars.</summary>
public interface IMarketDataClient
{
    /// <summary>
    /// Returns a top-of-book snapshot for one or more instruments.
    /// </summary>
    /// <param name="conIds">The instruments. At most 100 per request.</param>
    /// <param name="fields">The tick identifiers to return. At most 50 per request. See <see cref="MarketDataField"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// <para>
    /// <strong>The first call for an instrument returns no data.</strong> IBKR treats it as a
    /// pre-flight that starts the backend streaming the instrument, and only subsequent calls return
    /// values — snapshots are read from those open streams rather than from a cache. Send the
    /// pre-flight with every field that will later be wanted, then request the snapshot again.
    /// </para>
    /// <para>
    /// Each subscribed instrument consumes one of the account's market data lines, 100 by default.
    /// Release them with <see cref="UnsubscribeAsync"/> or <see cref="UnsubscribeAllAsync"/> rather
    /// than leaving streams open.
    /// </para>
    /// <para>Limited by IBKR to 10 requests per second.</para>
    /// </remarks>
    Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotAsync(
        IReadOnlyList<ConId> conIds,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns historical OHLC bars for an instrument.
    /// </summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="period">How far the request extends from its start time.</param>
    /// <param name="bar">The width of each bar. Need not divide <paramref name="period"/> evenly.</param>
    /// <param name="exchange">The exchange, or <c>SMART</c>.</param>
    /// <param name="outsideRegularTradingHours">Whether to include data from outside regular hours.</param>
    /// <param name="startTime">
    /// A fixed reference point for the request. When omitted, the current time is used and
    /// <paramref name="direction"/> must be left at its default.
    /// </param>
    /// <param name="direction">Whether the period extends backwards from or forwards to the start time.</param>
    /// <param name="source">The type of price the bars are built from.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>IBKR permits five concurrent historical data requests.</remarks>
    Task<HistoricalBars> GetHistoryAsync(
        ConId conId,
        HistoryPeriod period,
        BarSize bar,
        string? exchange = null,
        bool? outsideRegularTradingHours = null,
        Instant? startTime = null,
        HistoricalDataDirection? direction = null,
        HistoricalDataSource? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>Closes the backend market data stream for one instrument.</summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<UnsubscribeResponse> UnsubscribeAsync(ConId conId, CancellationToken cancellationToken = default);

    /// <summary>Closes every backend market data stream.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<UnsubscribeAllResponse> UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}
