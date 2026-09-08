using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.PortfolioAnalyst;

/// <summary>
/// How a portfolio is spread across a category, as returned by <c>POST /pa/allocation</c>.
/// </summary>
public sealed record PortfolioAllocation
{
    /// <summary>The request identifier, <c>getAllocation</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The currency the net asset values are denominated in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>Whether the figures are for the current day rather than a closed one.</summary>
    /// <remarks>
    /// Current-day figures are only available when every requested account has the same base
    /// currency as the one asked for. When they are not, IBKR falls back to the previous business
    /// day rather than failing.
    /// </remarks>
    [JsonPropertyName("realtime")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRealTime { get; init; }

    /// <summary>The day the figures are as of.</summary>
    /// <remarks>
    /// Documented without qualification, but present only when <see cref="IsRealTime"/> is false. A
    /// live gateway omits it entirely for current-day figures -- they are as of now, and IBKR sends
    /// no date for now -- and sends it alongside <c>realtime: false</c> whenever a past date was
    /// asked for.
    /// </remarks>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The breakdowns, keyed by category.</summary>
    /// <remarks>
    /// Holds one entry for the requested category, or one per category when
    /// <see cref="PortfolioAllocationType.All"/> was asked for. Keyed by text rather than by
    /// <see cref="PortfolioAllocationType"/> so that a category IBKR adds later arrives rather than
    /// failing the response; <see cref="GetBreakdown(PortfolioAllocationType)"/> looks one up.
    /// </remarks>
    [JsonPropertyName("allocations")]
    public IReadOnlyDictionary<string, AllocationBreakdown> Allocations { get; init; } =
        new Dictionary<string, AllocationBreakdown>(StringComparer.Ordinal);

    /// <summary>The requested accounts that could not be included.</summary>
    [JsonPropertyName("excluded")]
    public IReadOnlyList<AccountId> Excluded { get; init; } = [];

    /// <summary>IBKR's warning about the result, where it has one.</summary>
    [JsonPropertyName("warning")]
    public string? Warning { get; init; }

    /// <summary>Returns one category's breakdown, or <see langword="null"/> when it is absent.</summary>
    /// <param name="type">The category to look up.</param>
    /// <remarks>
    /// <see cref="PortfolioAllocationType.All"/> is a request parameter rather than a category, and
    /// never matches.
    /// </remarks>
    public AllocationBreakdown? GetBreakdown(PortfolioAllocationType type) =>
        GetBreakdown(type.ToWireValue());

    /// <summary>Returns one category's breakdown, or <see langword="null"/> when it is absent.</summary>
    /// <param name="type">The category name, matched case-insensitively.</param>
    public AllocationBreakdown? GetBreakdown(string type)
    {
        foreach (var pair in Allocations)
        {
            if (string.Equals(pair.Key, type, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// One category's allocation, split by direction.
/// </summary>
public sealed record AllocationBreakdown
{
    /// <summary>The long side of the portfolio.</summary>
    [JsonPropertyName("long")]
    public AllocationSide? LongPositions { get; init; }

    /// <summary>The short side of the portfolio.</summary>
    /// <remarks>Omitted entirely by a live gateway when the portfolio holds nothing short.</remarks>
    [JsonPropertyName("short")]
    public AllocationSide? ShortPositions { get; init; }
}

/// <summary>
/// One direction's allocation across a category.
/// </summary>
public sealed record AllocationSide
{
    /// <summary>The direction's totals.</summary>
    /// <remarks>
    /// <see cref="AllocationEntry.Weight"/> is <c>1</c> here: the weights within a direction are
    /// proportions of that direction, not of the whole portfolio, so the long and short sides each
    /// sum to one.
    /// </remarks>
    [JsonPropertyName("total")]
    public AllocationEntry? Total { get; init; }

    /// <summary>The categories held, one entry each.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<AllocationEntry> Items { get; init; } = [];
}

/// <summary>
/// One category's holding, or the total across them.
/// </summary>
public sealed record AllocationEntry
{
    /// <summary>The category's identifier, for example <c>STK</c>, <c>US</c> or the sector code <c>57</c>.</summary>
    /// <remarks>Absent on a total. IBKR does not publish the code sets; <see cref="Name"/> is the readable form.</remarks>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The category's English display name, for example <c>Technology</c>.</summary>
    /// <remarks>Absent on a total.</remarks>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The amount held, in <see cref="PortfolioAllocation.Currency"/>.</summary>
    /// <remarks>Negative on the short side.</remarks>
    [JsonPropertyName("nav")]
    public decimal? NetAssetValue { get; init; }

    /// <summary>The proportion of its direction held here, as a fraction rather than a percentage.</summary>
    /// <remarks><c>0.24488781</c> means 24.49%.</remarks>
    [JsonPropertyName("weight")]
    public decimal? Weight { get; init; }

    /// <summary>The colour IBKR draws the category in, as an RGB hex code such as <c>#008fd5</c>.</summary>
    /// <remarks>Absent on a total. Stable per category, so a chart drawn from it matches Client Portal's.</remarks>
    [JsonPropertyName("color")]
    public string? Color { get; init; }
}

/// <summary>
/// The category a portfolio allocation is broken down by, as accepted by the <c>type</c> field of
/// <c>POST /pa/allocation</c>.
/// </summary>
/// <remarks>
/// Sent in upper case, which is how IBKR keys the response. It reads the request case-insensitively
/// -- a live gateway accepted <c>sector</c> and answered under <c>SECTOR</c> -- but matching the
/// response's own casing means <see cref="PortfolioAllocation.GetBreakdown(PortfolioAllocationType)"/>
/// is looking for what it sent.
/// </remarks>
public enum PortfolioAllocationType
{
    /// <summary>By instrument kind, for example stocks against cash.</summary>
    [JsonStringEnumMemberName("FINANCIAL_INSTRUMENT")]
    FinancialInstrument,

    /// <summary>By asset class, for example equities against cash.</summary>
    [JsonStringEnumMemberName("ASSET_CLASS")]
    AssetClass,

    /// <summary>By industry sector.</summary>
    [JsonStringEnumMemberName("SECTOR")]
    Sector,

    /// <summary>By region.</summary>
    [JsonStringEnumMemberName("REGION")]
    Region,

    /// <summary>By country.</summary>
    [JsonStringEnumMemberName("COUNTRY")]
    Country,

    /// <summary>Every category at once.</summary>
    /// <remarks>
    /// A request parameter rather than a category: the response is keyed by the five real
    /// categories, and never by this.
    /// </remarks>
    [JsonStringEnumMemberName("ALL")]
    All,
}

/// <summary>
/// Conversions between <see cref="PortfolioAllocationType"/> and the text IBKR uses on the wire.
/// </summary>
public static class PortfolioAllocationTypeExtensions
{
    /// <summary>Returns the value IBKR uses on the wire, for example <c>ASSET_CLASS</c>.</summary>
    /// <param name="type">The allocation category.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined value.</exception>
    public static string ToWireValue(this PortfolioAllocationType type) => type switch
    {
        PortfolioAllocationType.FinancialInstrument => "FINANCIAL_INSTRUMENT",
        PortfolioAllocationType.AssetClass => "ASSET_CLASS",
        PortfolioAllocationType.Sector => "SECTOR",
        PortfolioAllocationType.Region => "REGION",
        PortfolioAllocationType.Country => "COUNTRY",
        PortfolioAllocationType.All => "ALL",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
