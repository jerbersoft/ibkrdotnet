using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>
/// The market rules governing orders for an instrument, from <c>POST /iserver/contract/rules</c> and
/// the <c>rules</c> member of <c>GET /iserver/contract/{conid}/info-and-rules</c>.
/// </summary>
/// <remarks>
/// Rules differ between order entry and order modification, so query them with
/// <c>modifyOrder</c> set when inspecting what a change to a working order is allowed to do.
/// </remarks>
public sealed record ContractRules
{
    /// <summary>Whether the instrument may be traded with an algorithm.</summary>
    [JsonPropertyName("algoEligible")]
    public bool? AlgoEligible { get; init; }

    /// <summary>Whether all-or-none orders are accepted.</summary>
    [JsonPropertyName("allOrNoneEligible")]
    public bool? AllOrNoneEligible { get; init; }

    /// <summary>Whether a cost report is produced.</summary>
    [JsonPropertyName("costReport")]
    public bool? CostReport { get; init; }

    /// <summary>The accounts permitted to trade the instrument.</summary>
    [JsonPropertyName("canTradeAcctIds")]
    public IReadOnlyList<AccountId> TradableAccountIds { get; init; } = [];

    /// <summary>The order types accepted during regular trading hours.</summary>
    [JsonPropertyName("orderTypes")]
    public IReadOnlyList<string> OrderTypes { get; init; } = [];

    /// <summary>The order types accepted outside regular trading hours.</summary>
    [JsonPropertyName("orderTypesOutside")]
    public IReadOnlyList<string> OrderTypesOutsideRegularHours { get; init; } = [];

    /// <summary>The IBKR algorithm types available.</summary>
    [JsonPropertyName("ibAlgoTypes")]
    public IReadOnlyList<string> IbAlgoTypes { get; init; } = [];

    /// <summary>The order types that support fractional quantities.</summary>
    [JsonPropertyName("fraqTypes")]
    public IReadOnlyList<string> FractionalOrderTypes { get; init; } = [];

    /// <summary>The order types that support cash quantities.</summary>
    [JsonPropertyName("cqtTypes")]
    public IReadOnlyList<string> CashQuantityOrderTypes { get; init; } = [];

    /// <summary>The times in force accepted.</summary>
    [JsonPropertyName("tifTypes")]
    public IReadOnlyList<string> TimeInForceTypes { get; init; } = [];

    /// <summary>
    /// Default order settings, keyed by order type and then by setting.
    /// </summary>
    /// <remarks>
    /// Nested, unlike <see cref="TimeInForceDefaults"/>: IBKR returns
    /// <c>{"LMT": {"LP": "198.22"}}</c> here but a flat map there.
    /// </remarks>
    [JsonPropertyName("orderDefaults")]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? OrderDefaults { get; init; }

    /// <summary>
    /// Default time-in-force settings, as a flat map. Boolean values arrive as JSON booleans.
    /// </summary>
    [JsonPropertyName("tifDefaults")]
    public IReadOnlyDictionary<string, string>? TimeInForceDefaults { get; init; }

    /// <summary>
    /// Whether IBKR requires the order be previewed before submission.
    /// </summary>
    /// <remarks>
    /// When set, submitting without a preview is rejected, so call
    /// <c>POST /iserver/account/{accountId}/orders/whatif</c> first.
    /// </remarks>
    [JsonPropertyName("forceOrderPreview")]
    public bool? ForceOrderPreview { get; init; }

    /// <summary>Whether a preview is available.</summary>
    [JsonPropertyName("preview")]
    public bool? Preview { get; init; }

    /// <summary>The default order size.</summary>
    [JsonPropertyName("defaultSize")]
    public decimal? DefaultSize { get; init; }

    /// <summary>The default cash size.</summary>
    [JsonPropertyName("cashSize")]
    public decimal? CashSize { get; init; }

    /// <summary>The size increment orders must be a multiple of.</summary>
    [JsonPropertyName("sizeIncrement")]
    public decimal? SizeIncrement { get; init; }

    /// <summary>The displayed size, where applicable.</summary>
    [JsonPropertyName("displaySize")]
    public decimal? DisplaySize { get; init; }

    /// <summary>The number of decimal places permitted in a fractional quantity.</summary>
    [JsonPropertyName("fraqInt")]
    public int? FractionalDecimals { get; init; }

    /// <summary>The currency cash quantities are expressed in.</summary>
    [JsonPropertyName("cashCcy")]
    public string? CashCurrency { get; init; }

    /// <summary>The cash quantity increment.</summary>
    [JsonPropertyName("cashQtyIncr")]
    public decimal? CashQuantityIncrement { get; init; }

    /// <summary>The default limit price.</summary>
    [JsonPropertyName("limitPrice")]
    public decimal? LimitPrice { get; init; }

    /// <summary>The default stop price.</summary>
    [JsonPropertyName("stopprice")]
    public decimal? StopPrice { get; init; }

    /// <summary>The price increment.</summary>
    [JsonPropertyName("increment")]
    public decimal? Increment { get; init; }

    /// <summary>The number of decimal places in the price increment.</summary>
    [JsonPropertyName("incrementDigits")]
    public int? IncrementDigits { get; init; }

    /// <summary>The increment type.</summary>
    [JsonPropertyName("incrementType")]
    public int? IncrementType { get; init; }

    /// <summary>The price increments by price band.</summary>
    [JsonPropertyName("incrementRules")]
    public IReadOnlyList<IncrementRule> IncrementRules { get; init; } = [];

    /// <summary>A price magnifier, where applicable.</summary>
    [JsonPropertyName("priceMagnifier")]
    public decimal? PriceMagnifier { get; init; }

    /// <summary>Whether the instrument can trade at a negative price.</summary>
    [JsonPropertyName("negativeCapable")]
    public bool? NegativeCapable { get; init; }

    /// <summary>Whether a secondary price is used.</summary>
    [JsonPropertyName("hasSecondary")]
    public bool? HasSecondary { get; init; }

    /// <summary>The order origination, where applicable.</summary>
    [JsonPropertyName("orderOrigination")]
    public string? OrderOrigination { get; init; }

    /// <summary>Any error IBKR reported while assembling the rules.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>The body of <c>POST /iserver/contract/rules</c>.</summary>
/// <param name="ConId">The instrument to inspect.</param>
/// <param name="IsBuy">Which side of the market the rules apply to.</param>
/// <param name="ModifyOrder">Whether the rules are for modifying an existing order.</param>
/// <param name="OrderId">The order being modified, when <paramref name="ModifyOrder"/> is set.</param>
internal sealed record ContractRulesRequest(
    [property: JsonPropertyName("conid")] long ConId,
    [property: JsonPropertyName("isBuy")] bool IsBuy = true,
    [property: JsonPropertyName("modifyOrder")] bool ModifyOrder = false,
    [property: JsonPropertyName("orderId")] long? OrderId = null);

/// <summary>The response from <c>GET /iserver/contract/{conid}/algos</c>.</summary>
public sealed record InstrumentAlgorithms
{
    /// <summary>The algorithms available for the instrument.</summary>
    [JsonPropertyName("algos")]
    public IReadOnlyList<TradingAlgorithm> Algorithms { get; init; } = [];
}

/// <summary>An execution algorithm available for an instrument.</summary>
public sealed record TradingAlgorithm
{
    /// <summary>The algorithm identifier, used as an order's <c>strategy</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The algorithm's display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>A description of the algorithm, when requested.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The algorithm's parameters, when requested.</summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyList<AlgorithmParameter> Parameters { get; init; } = [];
}

/// <summary>A parameter of an execution algorithm.</summary>
public sealed record AlgorithmParameter
{
    /// <summary>The parameter's identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The parameter's display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The parameter's type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The parameter's default value.</summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }
}

/// <summary>The response from <c>GET /iserver/secdef/bond-filters</c>.</summary>
public sealed record BondFilters
{
    /// <summary>The filters available for the issuer's bonds.</summary>
    [JsonPropertyName("bondFilters")]
    public IReadOnlyList<BondFilter> Filters { get; init; } = [];
}

/// <summary>One filter available when searching an issuer's bonds.</summary>
public sealed record BondFilter
{
    /// <summary>The filter's display label, for example <c>Maturity Date</c>.</summary>
    [JsonPropertyName("displayText")]
    public string? DisplayText { get; init; }

    /// <summary>The column the filter applies to.</summary>
    [JsonPropertyName("columnId")]
    public int? ColumnId { get; init; }

    /// <summary>The values the filter accepts.</summary>
    [JsonPropertyName("options")]
    public IReadOnlyList<BondFilterOption> Options { get; init; } = [];
}

/// <summary>One value a bond filter accepts.</summary>
public sealed record BondFilterOption
{
    /// <summary>The value to send.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>The value's display label.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>The response from <c>GET /trsrv/secdef</c>.</summary>
public sealed record InstrumentDefinitions
{
    /// <summary>The instrument definitions.</summary>
    [JsonPropertyName("secdef")]
    public IReadOnlyList<InstrumentDefinition> Definitions { get; init; } = [];
}
