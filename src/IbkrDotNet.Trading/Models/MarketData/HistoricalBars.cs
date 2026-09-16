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
/// <remarks>
/// A bar's volume arrives divided by <see cref="VolumeFactor"/>, which differs from one response to
/// the next. The division is undone here: <see cref="HistoricalBar.Volume"/> is a count of shares
/// and <see cref="HistoricalBar.RawVolume"/> is the number as IBKR sent it. Prices are untouched.
/// </remarks>
public sealed record HistoricalBars
{
    private IReadOnlyList<HistoricalBar> _bars = [];
    private decimal? _volumeFactor;

    /// <summary>The instrument's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>A description of the instrument, or the company name.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The bars.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<HistoricalBar> Bars
    {
        get => _bars;
        init => _bars = Scaled(value, _volumeFactor);
    }

    /// <summary>
    /// What IBKR divided each bar's volume by before sending it. 40 and 100 have both been seen, on
    /// the same instrument; some responses carry no factor at all.
    /// </summary>
    /// <remarks>
    /// Already applied — <see cref="HistoricalBar.Volume"/> is a share count — and carried so the
    /// arithmetic can be checked, and so anything read out of the raw response by hand can be put on
    /// the same scale.
    /// </remarks>
    [JsonPropertyName("volumeFactor")]
    public decimal? VolumeFactor
    {
        get => _volumeFactor;
        init
        {
            _volumeFactor = value;

            // The factor arrives before the bars in one recorded answer and after them in IBKR's
            // published example, so it is stamped from both accessors and the order stops mattering.
            // Stamping is idempotent: a bar keeps the number IBKR sent and multiplies on read.
            _bars = Scaled(_bars, value);
        }
    }

    /// <summary>What the envelope's <c>high</c> and <c>low</c> summary strings were divided by.</summary>
    /// <remarks>
    /// It does not touch a bar: <c>o</c>, <c>h</c>, <c>l</c> and <c>c</c> arrive as real prices and
    /// dividing them by this would be wrong. It scales the first field of the two summary strings —
    /// <c>33622/847829.15/36000</c> is a high of 336.22, the unscaled volume of the bar that set it,
    /// and the minutes from <see cref="StartTime"/> to the end of that bar. Those strings are not
    /// modelled, because every number in them is in <see cref="Bars"/> already.
    /// </remarks>
    [JsonPropertyName("priceFactor")]
    public decimal? PriceFactor { get; init; }

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

    /// <summary>Stamps the response's volume factor on each bar.</summary>
    private static IReadOnlyList<HistoricalBar> Scaled(IReadOnlyList<HistoricalBar>? bars, decimal? factor)
    {
        if (bars is null || bars.Count == 0)
        {
            return [];
        }

        // A factor IBKR did not send, or one of zero, leaves the volume as it arrived. Zero is not a
        // scale, and silently reporting every bar as having traded nothing is the worse answer.
        var applied = factor is { } value && value > 0m ? value : 1m;

        return bars.All(bar => bar.VolumeFactor == applied)
            ? bars
            : [.. bars.Select(bar => bar with { VolumeFactor = applied })];
    }
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

    /// <summary>The volume as IBKR sent it, which is not a count of shares.</summary>
    /// <remarks>
    /// The share count divided by <see cref="HistoricalBars.VolumeFactor"/>. <see cref="Volume"/> is
    /// the number to read; this one is here to be matched against the response's own <c>high</c> and
    /// <c>low</c> summary strings, which quote the volume unscaled.
    /// </remarks>
    [JsonPropertyName("v")]
    public decimal? RawVolume { get; init; }

    /// <summary>The volume, in shares. Returned only when the source is <c>Last</c>.</summary>
    /// <remarks>
    /// <see cref="RawVolume"/> multiplied by the <see cref="HistoricalBars.VolumeFactor"/> of the
    /// response the bar arrived in — 40 in one live response and 100 in another, a difference IBKR
    /// makes nothing of. A bar deserialized on its own, or one from a response that sent no factor,
    /// reads its volume exactly as sent.
    /// </remarks>
    [JsonIgnore]
    public decimal? Volume => RawVolume * VolumeFactor;

    /// <summary>
    /// The factor stamped on the bar by the response it arrived in, and one when that response
    /// carried none.
    /// </summary>
    [JsonIgnore]
    internal decimal VolumeFactor { get; init; } = 1m;
}
