using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.PortfolioAnalyst;

/// <summary>
/// A window PortfolioAnalyst can report performance over, as accepted by the <c>period</c> field of
/// <c>POST /pa/performance</c>.
/// </summary>
/// <remarks>
/// <para>
/// IBKR documents two different sets of values for this field and they do not agree. The API
/// reference offers <c>1D</c>, <c>7D</c>, <c>MTD</c>, <c>1M</c>, <c>3M</c>, <c>6M</c>, <c>12M</c>
/// and <c>YTD</c>; the endpoint guide offers <c>1D</c>, <c>7D</c>, <c>MTD</c>, <c>1M</c>, <c>YTD</c>
/// and <c>1Y</c>. The set below is the one a live gateway accepts, which is neither in full.
/// </para>
/// <para>
/// The reference's extra three are not merely ignored. A live gateway validates the field and
/// rejects them by name: <c>3M</c> is answered <c>400 Bad Request: Invalid period: 3M</c>. Offering
/// them here would have cost a caller the endpoint's whole fifteen-minute window to discover that
/// IBKR's own reference was wrong about its own field. The six below are the endpoint guide's set,
/// and are also the <c>periods</c> array a live <c>/pa/allperiods</c> reports for itself.
/// </para>
/// </remarks>
public enum PerformancePeriod
{
    /// <summary>The last 24 hours.</summary>
    [JsonStringEnumMemberName("1D")]
    OneDay,

    /// <summary>The last seven full days.</summary>
    [JsonStringEnumMemberName("7D")]
    SevenDays,

    /// <summary>Since the first of the month.</summary>
    [JsonStringEnumMemberName("MTD")]
    MonthToDate,

    /// <summary>One full calendar month back from the last full trading day.</summary>
    [JsonStringEnumMemberName("1M")]
    OneMonth,

    /// <summary>Since the first of January.</summary>
    [JsonStringEnumMemberName("YTD")]
    YearToDate,

    /// <summary>One full year back from the last full trading day.</summary>
    [JsonStringEnumMemberName("1Y")]
    OneYear,
}

/// <summary>
/// Conversions between <see cref="PerformancePeriod"/> and the text IBKR uses on the wire.
/// </summary>
public static class PerformancePeriodExtensions
{
    /// <summary>Returns the value IBKR uses on the wire, for example <c>MTD</c>.</summary>
    /// <param name="period">The period.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is not a defined value.</exception>
    public static string ToWireValue(this PerformancePeriod period) => period switch
    {
        PerformancePeriod.OneDay => "1D",
        PerformancePeriod.SevenDays => "7D",
        PerformancePeriod.MonthToDate => "MTD",
        PerformancePeriod.OneMonth => "1M",
        PerformancePeriod.YearToDate => "YTD",
        PerformancePeriod.OneYear => "1Y",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
    };
}
