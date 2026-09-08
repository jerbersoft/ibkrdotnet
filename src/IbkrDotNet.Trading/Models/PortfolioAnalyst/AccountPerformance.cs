using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.PortfolioAnalyst;

/// <summary>
/// The mark-to-market performance of one or more accounts over a single period, as returned by
/// <c>POST /pa/performance</c>.
/// </summary>
/// <remarks>
/// The three series are parallel views of the same window: <see cref="NetAssetValue"/> holds the
/// account's value, <see cref="CumulativePerformance"/> the return since the start of the period,
/// and <see cref="TimePeriodPerformance"/> the return within each interval. They do not share a
/// frequency -- a live gateway answers a one-month request with daily NAV and cumulative figures
/// alongside monthly interval returns -- so each carries its own dates.
/// </remarks>
public sealed record AccountPerformance
{
    /// <summary>The request identifier, <c>getPerformanceData</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>IBKR's internal data identifier.</summary>
    /// <remarks>Documented as "Internal Use Only"; a live gateway omits it entirely.</remarks>
    [JsonPropertyName("rc")]
    public long? ResponseCode { get; init; }

    /// <summary>Roughly the width of the window, in calendar days.</summary>
    /// <remarks>
    /// <para>
    /// IBKR documents this as "the total data points", which it demonstrably is not: a live gateway
    /// answered a one-month request with <c>33</c> against 22 points in the series, and a one-year
    /// request with <c>366</c> against 262. Both are close to the calendar width of the window
    /// rather than to any array's length.
    /// </para>
    /// <para>
    /// Close, but not by a rule that survives both samples. The year's <c>366</c> is its
    /// <see cref="PerformanceSeriesEntry.Start"/> and <see cref="PerformanceSeriesEntry.End"/>
    /// counted inclusively; the month's <c>33</c> is one more than the same sum. Treat it as an
    /// approximate width and take the length of a series from the series.
    /// </para>
    /// </remarks>
    [JsonPropertyName("nd")]
    public long? DayCount { get; init; }

    /// <summary>The portfolio measure the returns were computed with, <c>TWR</c> or <c>MWR</c>.</summary>
    /// <remarks>
    /// Time-weighted and money-weighted return respectively. The measure is a PortfolioAnalyst
    /// account setting, not a request parameter, so it is reported rather than chosen here.
    /// </remarks>
    [JsonPropertyName("pm")]
    public string? PortfolioMeasure { get; init; }

    /// <summary>The currency the figures are denominated in, <c>base</c> when it is the account's own.</summary>
    [JsonPropertyName("currencyType")]
    public string? CurrencyType { get; init; }

    /// <summary>The accounts that were consolidated into the result.</summary>
    [JsonPropertyName("included")]
    public IReadOnlyList<AccountId> Included { get; init; } = [];

    /// <summary>The account's net asset value over the period.</summary>
    /// <remarks>Not populated for benchmarks, which have no net asset value.</remarks>
    [JsonPropertyName("nav")]
    public PerformanceSeries? NetAssetValue { get; init; }

    /// <summary>The cumulative return since the start of the period.</summary>
    [JsonPropertyName("cps")]
    public PerformanceSeries? CumulativePerformance { get; init; }

    /// <summary>The return within each interval of the period, rather than since its start.</summary>
    /// <remarks>
    /// This is the series whose <see cref="PerformanceSeries.Frequency"/> most often differs from
    /// the others, and so the one whose <see cref="PerformanceSeries.Dates"/> most often are not
    /// calendar dates.
    /// </remarks>
    [JsonPropertyName("tpps")]
    public PerformanceSeries? TimePeriodPerformance { get; init; }
}

/// <summary>
/// One series of performance figures, together with the labels its values line up against.
/// </summary>
/// <remarks>
/// <see cref="Dates"/> is parallel to the <see cref="PerformanceSeriesEntry.Returns"/> and
/// <see cref="PerformanceSeriesEntry.Values"/> of every entry in <see cref="Data"/>: the nth label
/// describes the nth number.
/// </remarks>
public sealed record PerformanceSeries
{
    /// <summary>How often the series is sampled -- <c>D</c> for daily, <c>M</c> for monthly.</summary>
    /// <remarks>
    /// Left as text because IBKR documents neither the set of values nor their meaning; <c>D</c> and
    /// <c>M</c> are the two a live gateway has produced. It matters more than a descriptive field
    /// usually would, because it determines how <see cref="Dates"/> is encoded.
    /// </remarks>
    [JsonPropertyName("freq")]
    public string? Frequency { get; init; }

    /// <summary>The labels the series' values line up against, in IBKR's encoding.</summary>
    /// <remarks>
    /// Text rather than <see cref="LocalDate"/>, because these are not always dates. The encoding
    /// follows <see cref="Frequency"/>: a daily series sends <c>yyyyMMdd</c> ("20260908"), and a
    /// monthly one sends <c>yyyyMM</c> ("202609") -- IBKR's own published example and a live gateway
    /// agree on both. Parsing them eagerly would mean either failing on a monthly series or
    /// inventing a day of the month that IBKR did not send.
    /// </remarks>
    [JsonPropertyName("dates")]
    public IReadOnlyList<string> Dates { get; init; } = [];

    /// <summary>The figures, one entry per account or benchmark.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<PerformanceSeriesEntry> Data { get; init; } = [];
}

/// <summary>
/// One account's or benchmark's figures within a <see cref="PerformanceSeries"/>.
/// </summary>
public sealed record PerformanceSeriesEntry
{
    /// <summary>What the entry describes, as identified by <see cref="IdType"/>.</summary>
    /// <remarks>
    /// Text rather than an <see cref="AccountId"/>: it holds an account identifier only while
    /// <see cref="IdType"/> is <c>acctid</c>, and a benchmark is identified differently.
    /// </remarks>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>How <see cref="Id"/> should be read, for example <c>acctid</c>.</summary>
    [JsonPropertyName("idType")]
    public string? IdType { get; init; }

    /// <summary>The first day of the window the figures cover.</summary>
    /// <remarks>
    /// Earlier than the first entry in <see cref="PerformanceSeries.Dates"/>: the window opens on
    /// the day <see cref="StartValue"/> was measured, and the first return is the move away from it.
    /// </remarks>
    [JsonPropertyName("start")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Start { get; init; }

    /// <summary>The last day of the window the figures cover.</summary>
    [JsonPropertyName("end")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? End { get; init; }

    /// <summary>The currency the figures are denominated in.</summary>
    [JsonPropertyName("baseCurrency")]
    public string? BaseCurrency { get; init; }

    /// <summary>The returns, as fractions rather than percentages.</summary>
    /// <remarks>
    /// <c>0.0639</c> means 6.39%. IBKR's published example quotes these as strings and a live
    /// gateway sends them as JSON numbers, so both are accepted.
    /// </remarks>
    [JsonPropertyName("returns")]
    public IReadOnlyList<decimal> Returns { get; init; } = [];

    /// <summary>The net asset values, present only on the net asset value series.</summary>
    [JsonPropertyName("navs")]
    public IReadOnlyList<decimal> Values { get; init; } = [];

    /// <summary>The net asset value the window opened from.</summary>
    [JsonPropertyName("startNAV")]
    public PerformanceStartValue? StartValue { get; init; }
}

/// <summary>
/// The net asset value a performance window opened from, and the day it was measured.
/// </summary>
public sealed record PerformanceStartValue
{
    /// <summary>The day the value was measured.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The net asset value.</summary>
    /// <remarks>
    /// Documented as an integer, which it is not: a live gateway answers with
    /// <c>81030.226542</c>.
    /// </remarks>
    [JsonPropertyName("val")]
    public decimal? Value { get; init; }
}
