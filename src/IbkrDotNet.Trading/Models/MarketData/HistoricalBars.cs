using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.MarketData;

/// <summary>The type of price a historical bar is built from.</summary>
public enum HistoricalDataSource
{
    /// <summary>Traded prices. The default.</summary>
    [JsonStringEnumMemberName("Last")]
    Last,

    /// <summary>Bid and ask prices.</summary>
    [JsonStringEnumMemberName("Bid_Ask")]
    BidAsk,

    /// <summary>The midpoint of the bid and ask.</summary>
    [JsonStringEnumMemberName("Midpoint")]
    Midpoint,
}

/// <summary>Which way a historical data request extends from its start time.</summary>
public enum HistoricalDataDirection
{
    /// <summary>The period ends at the start time, extending backwards. The default.</summary>
    EndingAtStartTime = -1,

    /// <summary>The period begins at the start time, extending forwards.</summary>
    StartingAtStartTime = 1,
}

/// <summary>
/// Historical OHLC bars, from <c>GET /iserver/marketdata/history</c>.
/// </summary>
public sealed record HistoricalBars
{
    /// <summary>The instrument's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>A description of the instrument, or the company name.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The bars.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HistoricalBar> Bars { get; init; } = [];

    /// <summary>The number of bars returned.</summary>
    [JsonPropertyName("points")]
    public long? Points { get; init; }

    /// <summary>The start of the complete period covered.</summary>
    [JsonPropertyName("startTime")]
    [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
    public Instant? StartTime { get; init; }

    /// <summary>The period the caller requested.</summary>
    [JsonPropertyName("timePeriod")]
    public string? TimePeriod { get; init; }

    /// <summary>The width of each bar.</summary>
    [JsonPropertyName("barLength")]
    [JsonConverter(typeof(DurationSecondsConverter))]
    public Duration? BarLength { get; init; }

    /// <summary>The length of the instrument's trading day.</summary>
    [JsonPropertyName("tradingDayDuration")]
    [JsonConverter(typeof(DurationSecondsConverter))]
    public Duration? TradingDayDuration { get; init; }

    /// <summary>How long IBKR took to serve the request.</summary>
    [JsonPropertyName("mktDataDelay")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? MarketDataDelay { get; init; }

    /// <summary>Whether data from outside regular trading hours is included.</summary>
    [JsonPropertyName("outsideRth")]
    public bool? OutsideRegularTradingHours { get; init; }

    /// <summary>
    /// The three-character market data availability marker. See
    /// <see cref="MarketDataSnapshot.MarketDataAvailability"/>.
    /// </summary>
    [JsonPropertyName("mdAvailability")]
    public string? MarketDataAvailability { get; init; }

    /// <summary>Whether the instrument can trade at a negative price.</summary>
    [JsonPropertyName("negativeCapable")]
    public bool? NegativeCapable { get; init; }

    /// <summary>The UTC time used to centre Client Portal charts. Internal use.</summary>
    [JsonPropertyName("chartPanStartTime")]
    [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
    public Instant? ChartPanStartTime { get; init; }

    /// <summary>IBKR's identifier for the request. Internal use.</summary>
    [JsonPropertyName("serverId")]
    public string? ServerId { get; init; }

    /// <summary>How long the request spent in flight. Internal use.</summary>
    [JsonPropertyName("travelTime")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? TravelTime { get; init; }
}

/// <summary>One OHLC bar.</summary>
/// <remarks>
/// What the prices mean depends on the request's source: traded prices for <c>Last</c>, the time
/// average bid and ask for <c>Bid_Ask</c>, and the midpoint for <c>Midpoint</c>. Volume is returned
/// only for <c>Last</c>.
/// </remarks>
public sealed record HistoricalBar
{
    /// <summary>The start of the bar.</summary>
    [JsonPropertyName("t")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? Start { get; init; }

    /// <summary>The opening value.</summary>
    [JsonPropertyName("o")]
    public decimal? Open { get; init; }

    /// <summary>The high value.</summary>
    [JsonPropertyName("h")]
    public decimal? High { get; init; }

    /// <summary>The low value.</summary>
    [JsonPropertyName("l")]
    public decimal? Low { get; init; }

    /// <summary>The closing value.</summary>
    [JsonPropertyName("c")]
    public decimal? Close { get; init; }

    /// <summary>The volume. Returned only when the source is <c>Last</c>.</summary>
    [JsonPropertyName("v")]
    public decimal? Volume { get; init; }
}
