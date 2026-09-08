using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Portfolio;

/// <summary>
/// One entry of <c>GET /portfolio/{accountId}/summary</c>.
/// </summary>
/// <remarks>
/// The response is a map from key (for example <c>netliquidation</c>, <c>accruedcash-s</c>) to one
/// of these. Numeric entries carry <see cref="Amount"/> with <see cref="Value"/> null, and textual
/// entries the reverse.
/// </remarks>
public sealed record PortfolioSummaryValue
{
    /// <summary>The numeric value, for numeric entries.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    /// <summary>The textual value, for textual entries.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>The currency, where IBKR reports one.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>Whether IBKR considers the entry null.</summary>
    [JsonPropertyName("isNull")]
    public bool? IsNull { get; init; }

    /// <summary>The message severity. Internal use.</summary>
    [JsonPropertyName("severity")]
    public int? Severity { get; init; }

    /// <summary>When the value was computed.</summary>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? Timestamp { get; init; }
}

/// <summary>The response from <c>GET /portfolio/{accountId}/allocation</c>.</summary>
/// <remarks>
/// Each grouping splits into long and short sides, keyed by the category name and holding the market
/// value in the account's base currency.
/// </remarks>
public sealed record AssetAllocation
{
    /// <summary>Allocation by asset class, for example <c>STK</c> and <c>OPT</c>.</summary>
    [JsonPropertyName("assetClass")]
    public AllocationSides? AssetClass { get; init; }

    /// <summary>Allocation by industry sub-category.</summary>
    [JsonPropertyName("group")]
    public AllocationSides? Group { get; init; }

    /// <summary>Allocation by industry sector.</summary>
    [JsonPropertyName("sector")]
    public AllocationSides? Sector { get; init; }
}

/// <summary>The long and short sides of one allocation grouping.</summary>
public sealed record AllocationSides
{
    /// <summary>Long market value by category.</summary>
    [JsonPropertyName("long")]
    public IReadOnlyDictionary<string, decimal> LongPositions { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Short market value by category, as negative amounts.</summary>
    [JsonPropertyName("short")]
    public IReadOnlyDictionary<string, decimal> ShortPositions { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);
}

/// <summary>A combination position, from <c>GET /portfolio/{accountId}/combo/positions</c>.</summary>
public sealed record ComboPosition
{
    /// <summary>The combination's description, for example <c>1*649180695-1*654503299</c>.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The combination's name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The legs making up the combination.</summary>
    [JsonPropertyName("legs")]
    public IReadOnlyList<ComboLeg> Legs { get; init; } = [];

    /// <summary>The underlying positions.</summary>
    [JsonPropertyName("positions")]
    public IReadOnlyList<Position> Positions { get; init; } = [];
}

/// <summary>One leg of a combination position.</summary>
public sealed record ComboLeg
{
    /// <summary>The leg instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public Primitives.ConId ConId { get; init; }

    /// <summary>The leg ratio. Negative for a short leg.</summary>
    [JsonPropertyName("ratio")]
    public decimal? Ratio { get; init; }
}

/// <summary>The response from <c>POST /portfolio/{accountId}/positions/invalidate</c>.</summary>
public sealed record InvalidatePositionCacheResponse
{
    /// <summary>The outcome message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
