using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Portfolio;

/// <summary>
/// A position from <c>GET /portfolio2/{accountId}/positions</c>.
/// </summary>
/// <remarks>
/// IBKR's newer positions endpoint returns a leaner, uncached view than
/// <c>/portfolio/{accountId}/positions/{pageId}</c>, with different field names for the same
/// quantities, so it is modelled separately rather than folded into <see cref="Position"/>.
/// </remarks>
public sealed record UncachedPosition
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The size of the position, in units of the instrument.</summary>
    [JsonPropertyName("position")]
    public decimal? Quantity { get; init; }

    /// <summary>A description of the instrument.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("assetClass")]
    public string? AssetClass { get; init; }

    /// <summary>The instrument's security type.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The average cost of the position.</summary>
    [JsonPropertyName("avgCost")]
    public decimal? AverageCost { get; init; }

    /// <summary>The average price of the position.</summary>
    [JsonPropertyName("avgPrice")]
    public decimal? AveragePrice { get; init; }

    /// <summary>The current market price.</summary>
    [JsonPropertyName("marketPrice")]
    public decimal? MarketPrice { get; init; }

    /// <summary>The current market value.</summary>
    [JsonPropertyName("marketValue")]
    public decimal? MarketValue { get; init; }

    /// <summary>Realized profit and loss.</summary>
    [JsonPropertyName("realizedPnl")]
    public decimal? RealizedPnl { get; init; }

    /// <summary>Unrealized profit and loss.</summary>
    [JsonPropertyName("unrealizedPnl")]
    public decimal? UnrealizedPnl { get; init; }

    /// <summary>The instrument's industry sector.</summary>
    [JsonPropertyName("sector")]
    public string? Sector { get; init; }

    /// <summary>The instrument's industry sub-category.</summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>The model portfolio contributing this position, where there is one.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Whether this is the last entry of the last page.</summary>
    [JsonPropertyName("isLastToLoq")]
    public bool? IsLastToLoq { get; init; }

    /// <summary>
    /// When the position was retrieved.
    /// </summary>
    /// <remarks>Epoch <em>seconds</em> on this endpoint.</remarks>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? RetrievedAt { get; init; }
}
