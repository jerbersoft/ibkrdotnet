using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IMarketDataClient" />
public sealed class MarketDataClient(IIbkrApiClient apiClient) : IMarketDataClient
{
    /// <summary>The most instruments IBKR accepts in one snapshot request.</summary>
    public const int MaxSnapshotConIds = 100;

    /// <summary>The most fields IBKR accepts in one snapshot request.</summary>
    public const int MaxSnapshotFields = 50;

    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotAsync(
        IReadOnlyList<ConId> conIds,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conIds);
        if (conIds.Count == 0)
        {
            throw new ArgumentException("At least one contract identifier is required.", nameof(conIds));
        }

        if (conIds.Count > MaxSnapshotConIds)
        {
            throw new ArgumentException(
                $"IBKR accepts at most {MaxSnapshotConIds} instruments per snapshot request; " +
                $"{conIds.Count} were supplied. Each subscribed instrument also consumes one of the " +
                "account's market data lines.",
                nameof(conIds));
        }

        if (fields is { Count: > MaxSnapshotFields })
        {
            throw new ArgumentException(
                $"IBKR accepts at most {MaxSnapshotFields} fields per snapshot request; " +
                $"{fields.Count} were supplied.",
                nameof(fields));
        }

        return _apiClient.SendAsync<IReadOnlyList<MarketDataSnapshot>>(
            IbkrRequest.Get("/v1/api/iserver/marketdata/snapshot")
                .WithCommaSeparatedQuery("conids", conIds.Select(c => c.ToString()))
                .WithCommaSeparatedQuery("fields", fields),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<HistoricalBars> GetHistoryAsync(
        ConId conId,
        HistoryPeriod period,
        BarSize bar,
        string? exchange = null,
        bool? outsideRegularTradingHours = null,
        Instant? startTime = null,
        HistoricalDataDirection? direction = null,
        HistoricalDataSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if (startTime is null && direction == HistoricalDataDirection.StartingAtStartTime)
        {
            throw new ArgumentException(
                "A forward direction requires a start time; IBKR only accepts it alongside one.",
                nameof(direction));
        }

        var request = IbkrRequest.Get("/v1/api/iserver/marketdata/history")
            .WithQuery("conid", conId.Value)
            .WithQuery("period", period.ToString())
            .WithQuery("bar", bar.ToString())
            .WithQuery("exchange", exchange)
            .WithQuery("outsideRth", outsideRegularTradingHours);

        if (startTime is { } start)
        {
            request.WithQuery("startTime", IbkrTimePatterns.UtcDateTime.Format(start.InUtc().LocalDateTime));
        }

        if (direction is { } value)
        {
            request.WithQuery("direction", (long)value);
        }

        if (source is { } dataSource)
        {
            request.WithQuery("source", dataSource switch
            {
                HistoricalDataSource.BidAsk => "Bid_Ask",
                HistoricalDataSource.Midpoint => "Midpoint",
                _ => "Last",
            });
        }

        return _apiClient.SendAsync<HistoricalBars>(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UnsubscribeResponse> UnsubscribeAsync(
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<UnsubscribeResponse>(
            IbkrRequest.Post("/v1/api/iserver/marketdata/unsubscribe")
                .WithJsonBody(new { conid = conId.Value }),
            cancellationToken);

    /// <inheritdoc />
    public Task<UnsubscribeAllResponse> UnsubscribeAllAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<UnsubscribeAllResponse>(
            IbkrRequest.Get("/v1/api/iserver/marketdata/unsubscribeall"),
            cancellationToken);
}
