using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>How market data for an instrument should be displayed.</summary>
public sealed record DisplayRule
{
    /// <summary>The display increments, by price band.</summary>
    [JsonPropertyName("displayRuleStep")]
    public IReadOnlyList<DisplayRuleStep> Steps { get; init; } = [];

    /// <summary>A magnifier applied to pricing, where applicable.</summary>
    [JsonPropertyName("magnification")]
    public long? Magnification { get; init; }
}

/// <summary>One band of a <see cref="DisplayRule"/>.</summary>
public sealed record DisplayRuleStep
{
    /// <summary>The number of decimal digits to display.</summary>
    [JsonPropertyName("decimalDigits")]
    public long? DecimalDigits { get; init; }

    /// <summary>The price at which this band takes effect.</summary>
    [JsonPropertyName("lowerEdge")]
    public decimal? LowerEdge { get; init; }

    /// <summary>The number of integer digits to display.</summary>
    [JsonPropertyName("wholeDigits")]
    public long? WholeDigits { get; init; }
}

/// <summary>The price increment that applies above a given price.</summary>
public sealed record IncrementRule
{
    /// <summary>The pricing increment.</summary>
    [JsonPropertyName("increment")]
    public decimal? Increment { get; init; }

    /// <summary>The price at which this increment takes effect.</summary>
    [JsonPropertyName("lowerEdge")]
    public decimal? LowerEdge { get; init; }
}
