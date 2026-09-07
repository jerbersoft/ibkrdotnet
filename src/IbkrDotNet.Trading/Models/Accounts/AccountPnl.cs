using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Accounts;

/// <summary>The response from <c>GET /iserver/account/pnl/partitioned</c>.</summary>
/// <remarks>
/// Limited to one request every five seconds. Keys of <see cref="Partitions"/> are account or model
/// identifiers suffixed with a partition name, for example <c>U1234567.Core</c>.
/// </remarks>
public sealed record AccountPnl
{
    /// <summary>
    /// Profit and loss per account partition, despite the field name also covering realized values.
    /// </summary>
    [JsonPropertyName("upnl")]
    public IReadOnlyDictionary<string, PnlPartition> Partitions { get; init; } =
        new Dictionary<string, PnlPartition>(StringComparer.Ordinal);
}

/// <summary>Profit and loss for one account partition.</summary>
public sealed record PnlPartition
{
    /// <summary>The partition's positional value. Always 1 for individual accounts.</summary>
    [JsonPropertyName("rowType")]
    public long? RowType { get; init; }

    /// <summary>Daily profit and loss.</summary>
    [JsonPropertyName("dpl")]
    public decimal? DailyPnl { get; init; }

    /// <summary>Net liquidity.</summary>
    [JsonPropertyName("nl")]
    public decimal? NetLiquidity { get; init; }

    /// <summary>Unrealized profit and loss.</summary>
    [JsonPropertyName("upl")]
    public decimal? UnrealizedPnl { get; init; }

    /// <summary>Excess liquidity.</summary>
    [JsonPropertyName("el")]
    public decimal? ExcessLiquidity { get; init; }

    /// <summary>Margin value.</summary>
    [JsonPropertyName("mv")]
    public decimal? MarginValue { get; init; }
}
