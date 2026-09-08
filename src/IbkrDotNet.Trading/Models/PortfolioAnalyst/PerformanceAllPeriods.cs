using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.PortfolioAnalyst;

/// <summary>
/// The performance of one or more accounts across every period PortfolioAnalyst tracks, as returned
/// by <c>POST /pa/allperiods</c>.
/// </summary>
/// <remarks>
/// <para>
/// The response is read by a converter rather than by property binding, because IBKR keys the
/// figures on the account identifier itself: alongside <c>pm</c>, <c>nd</c> and the rest sits a
/// property literally named <c>DU1234567</c> holding everything the call was made for. IBKR's own
/// reference does not mention that property at all -- it documents the seven fixed fields and stops
/// -- so a model that bound only what is documented would deserialize without error and return
/// nothing of value. <see cref="Accounts"/> is that property, collected by name.
/// </para>
/// <para>
/// Each account reports several periods at once, so unlike <c>/pa/performance</c> there is nothing
/// to choose: one call covers every period in <see cref="AccountPerformanceHistory.PeriodNames"/>.
/// </para>
/// </remarks>
[JsonConverter(typeof(PerformanceAllPeriodsConverter))]
public sealed record PerformanceAllPeriods
{
    /// <summary>The request identifier, <c>getPerformanceAllPeriods</c>.</summary>
    public string? Id { get; init; }

    /// <summary>IBKR's internal data identifier.</summary>
    /// <remarks>Documented as "Internal Use Only"; a live gateway omits it entirely.</remarks>
    public long? ResponseCode { get; init; }

    /// <summary>Roughly the width of the window, in calendar days.</summary>
    /// <remarks>
    /// See <see cref="AccountPerformance.DayCount"/>: IBKR calls this the total data points on every
    /// endpoint that sends it, and it is not that on any of them.
    /// </remarks>
    public long? DayCount { get; init; }

    /// <summary>The portfolio measure the returns were computed with, <c>TWR</c> or <c>MWR</c>.</summary>
    public string? PortfolioMeasure { get; init; }

    /// <summary>The currency the figures are denominated in, <c>base</c> when it is the account's own.</summary>
    public string? CurrencyType { get; init; }

    /// <summary>The accounts being viewed.</summary>
    public IReadOnlyList<AccountId> View { get; init; } = [];

    /// <summary>The accounts that were consolidated into the result.</summary>
    public IReadOnlyList<AccountId> Included { get; init; } = [];

    /// <summary>The figures, keyed by the account they describe.</summary>
    /// <remarks>
    /// Keyed rather than listed because that is how they arrive. Requesting several accounts
    /// consolidates them, so in practice this holds one entry whose key is the consolidation, not
    /// one entry per requested account.
    /// </remarks>
    public IReadOnlyDictionary<AccountId, AccountPerformanceHistory> Accounts { get; init; } =
        new Dictionary<AccountId, AccountPerformanceHistory>();
}

/// <summary>
/// One account's performance across every period PortfolioAnalyst tracks.
/// </summary>
/// <remarks>
/// Read by a converter for the same reason as its parent: the period names are property names, so
/// <c>1D</c>, <c>7D</c>, <c>MTD</c>, <c>1M</c>, <c>YTD</c> and <c>1Y</c> sit as siblings of
/// <c>baseCurrency</c> and <c>start</c> in one object. The periods are collected into
/// <see cref="Periods"/> and the rest bound by name.
/// </remarks>
[JsonConverter(typeof(AccountPerformanceHistoryConverter))]
public sealed record AccountPerformanceHistory
{
    /// <summary>The currency the figures are denominated in.</summary>
    public string? BaseCurrency { get; init; }

    /// <summary>The first day covered by the longest period reported.</summary>
    public LocalDate? Start { get; init; }

    /// <summary>The last day covered.</summary>
    public LocalDate? End { get; init; }

    /// <summary>When PortfolioAnalyst last recomputed these figures.</summary>
    /// <remarks>
    /// <para>
    /// IBKR does not state a time zone. It is read as UTC because a live gateway answers with UTC's
    /// wall clock rather than the host's: two requests, fired at 17:33:08 and 17:48:45 UTC from a
    /// machine on British Summer Time an hour ahead, came back reading <c>2026-09-08 17:33:08</c>
    /// and <c>2026-09-08 17:48:45</c>.
    /// </para>
    /// <para>
    /// Which is also to say the figures are recomputed on demand, and this reports when the call was
    /// answered rather than how stale the answer is. It is not a cache indicator.
    /// </para>
    /// </remarks>
    public Instant? LastSuccessfulUpdate { get; init; }

    /// <summary>The periods reported, in the order IBKR lists them.</summary>
    /// <remarks>
    /// The keys of <see cref="Periods"/>, which is an unordered map. IBKR orders them shortest
    /// first, and its list is authoritative over the reference documentation: it names <c>1Y</c>,
    /// which the reference for <c>/pa/performance</c> does not offer, and omits the <c>3M</c>,
    /// <c>6M</c> and <c>12M</c> that reference does.
    /// </remarks>
    public IReadOnlyList<string> PeriodNames { get; init; } = [];

    /// <summary>The figures, keyed by period name.</summary>
    public IReadOnlyDictionary<string, PerformancePeriodSeries> Periods { get; init; } =
        new Dictionary<string, PerformancePeriodSeries>(StringComparer.Ordinal);

    /// <summary>Returns one period's figures, or <see langword="null"/> when it is absent.</summary>
    /// <param name="period">The period to look up.</param>
    public PerformancePeriodSeries? GetPeriod(PerformancePeriod period) =>
        GetPeriod(period.ToWireValue());

    /// <summary>Returns one period's figures, or <see langword="null"/> when it is absent.</summary>
    /// <param name="period">The period name, matched case-insensitively.</param>
    public PerformancePeriodSeries? GetPeriod(string period)
    {
        foreach (var pair in Periods)
        {
            if (string.Equals(pair.Key, period, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// One period's net asset values and cumulative returns.
/// </summary>
/// <remarks>
/// The three arrays are parallel: the nth entry of <see cref="Dates"/>, <see cref="NetAssetValues"/>
/// and <see cref="CumulativeReturns"/> describe the same moment. This is a flatter shape than the
/// <see cref="PerformanceSeries"/> that <c>/pa/performance</c> returns, which nests the numbers one
/// level deeper so that several accounts can share a set of dates.
/// </remarks>
public sealed record PerformancePeriodSeries
{
    /// <summary>How often the period is sampled -- <c>D</c> for daily.</summary>
    /// <remarks>
    /// Every period a live gateway has returned is daily, including the year-long ones, which is why
    /// <c>1Y</c> arrives as 262 points rather than 12.
    /// </remarks>
    [JsonPropertyName("freq")]
    public string? Frequency { get; init; }

    /// <summary>The labels the values line up against, in IBKR's encoding.</summary>
    /// <remarks>
    /// Text for the reason given on <see cref="PerformanceSeries.Dates"/>: the encoding follows
    /// <see cref="Frequency"/>, and is <c>yyyyMMdd</c> only while that is <c>D</c>.
    /// </remarks>
    [JsonPropertyName("dates")]
    public IReadOnlyList<string> Dates { get; init; } = [];

    /// <summary>The net asset value at each point.</summary>
    [JsonPropertyName("nav")]
    public IReadOnlyList<decimal> NetAssetValues { get; init; } = [];

    /// <summary>The cumulative return at each point, as a fraction rather than a percentage.</summary>
    [JsonPropertyName("cps")]
    public IReadOnlyList<decimal> CumulativeReturns { get; init; } = [];

    /// <summary>The net asset value the period opened from.</summary>
    /// <remarks>
    /// Measured on the day before the first entry in <see cref="Dates"/>, so that the first return
    /// has something to be a return from.
    /// </remarks>
    [JsonPropertyName("startNAV")]
    public PerformanceStartValue? StartValue { get; init; }
}
