using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>
/// The status of one order, from <c>GET /iserver/account/order/status/{orderId}</c>.
/// </summary>
/// <remarks>
/// Snake-cased, unlike the camel-cased <see cref="LiveOrder"/> covering the same order.
/// </remarks>
public sealed record OrderStatus
{
    /// <summary>The IBKR-assigned order identifier.</summary>
    [JsonPropertyName("order_id")]
    public OrderId OrderId { get; init; }

    /// <summary>The account the order clears to.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The clearing account.</summary>
    [JsonPropertyName("order_clearing_account")]
    public string? ClearingAccount { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier and routing destination.</summary>
    [JsonPropertyName("conidex")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The instrument's description.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("sec_type")]
    public string? SecurityType { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The instrument's listing exchange.</summary>
    [JsonPropertyName("listing_exchange")]
    public string? ListingExchange { get; init; }

    /// <summary>The side of the order.</summary>
    [JsonPropertyName("side")]
    public string? Side { get; init; }

    /// <summary>The order's status, for example <c>Filled</c>.</summary>
    [JsonPropertyName("order_status")]
    public string? Status { get; init; }

    /// <summary>A human-readable description of the status.</summary>
    [JsonPropertyName("order_status_description")]
    public string? StatusDescription { get; init; }

    /// <summary>The order's status at the central counterparty.</summary>
    [JsonPropertyName("order_ccp_status")]
    public string? ClearingStatus { get; init; }

    /// <summary>The order type.</summary>
    [JsonPropertyName("order_type")]
    public string? OrderType { get; init; }

    /// <summary>The time in force.</summary>
    [JsonPropertyName("tif")]
    public string? TimeInForce { get; init; }

    /// <summary>A human-readable description of the order.</summary>
    [JsonPropertyName("order_description")]
    public string? OrderDescription { get; init; }

    /// <summary>A human-readable description of the order including its instrument.</summary>
    [JsonPropertyName("order_description_with_contract")]
    public string? OrderDescriptionWithContract { get; init; }

    /// <summary>The quantity still working.</summary>
    [JsonPropertyName("size")]
    public decimal? Size { get; init; }

    /// <summary>The total order size.</summary>
    [JsonPropertyName("total_size")]
    public decimal? TotalSize { get; init; }

    /// <summary>The cumulative filled quantity.</summary>
    [JsonPropertyName("cum_fill")]
    public decimal? CumulativeFill { get; init; }

    /// <summary>Size and fills, as displayed.</summary>
    [JsonPropertyName("size_and_fills")]
    public string? SizeAndFills { get; init; }

    /// <summary>The average fill price.</summary>
    [JsonPropertyName("average_price")]
    public decimal? AveragePrice { get; init; }

    /// <summary>The order's limit price, where applicable.</summary>
    [JsonPropertyName("limit_price")]
    public decimal? LimitPrice { get; init; }

    /// <summary>The order's stop price, where applicable.</summary>
    [JsonPropertyName("stop_price")]
    public decimal? StopPrice { get; init; }

    /// <summary>When the order was submitted.</summary>
    /// <remarks>IBKR sends this in <c>YYMMDDhhmmss</c>, resolved into the 2000s.</remarks>
    [JsonPropertyName("order_time")]
    [JsonConverter(typeof(IbkrCompactDateTimeConverter))]
    public Instant? OrderTime { get; init; }

    /// <summary>Whether the order can no longer be cancelled.</summary>
    [JsonPropertyName("cannot_cancel_order")]
    public bool? CannotCancelOrder { get; init; }

    /// <summary>Whether the order can no longer be modified.</summary>
    [JsonPropertyName("order_not_editable")]
    public bool? NotEditable { get; init; }

    /// <summary>The fields that may still be modified.</summary>
    [JsonPropertyName("editable_fields")]
    public string? EditableFields { get; init; }

    /// <summary>Whether the order is deactivated.</summary>
    [JsonPropertyName("deactivate_order")]
    public bool? Deactivated { get; init; }

    /// <summary>IBKR's request identifier.</summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }
}

/// <summary>
/// An execution against an order, from <c>GET /iserver/account/trades</c>.
/// </summary>
public sealed record Execution
{
    /// <summary>The IBKR-assigned execution identifier.</summary>
    [JsonPropertyName("execution_id")]
    public string? ExecutionId { get; init; }

    /// <summary>The order the execution belongs to.</summary>
    [JsonPropertyName("order_id")]
    public OrderId? OrderId { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier and routing destination.</summary>
    [JsonPropertyName("conidEx")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The instrument's description.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("sec_type")]
    public string? SecurityType { get; init; }

    /// <summary>The side of the execution: <c>B</c> or <c>S</c>.</summary>
    [JsonPropertyName("side")]
    public string? Side { get; init; }

    /// <summary>A human-readable description of the execution.</summary>
    [JsonPropertyName("order_description")]
    public string? OrderDescription { get; init; }

    /// <summary>
    /// When the execution occurred.
    /// </summary>
    /// <remarks>
    /// Read from <c>trade_time_r</c>, epoch milliseconds. IBKR also sends <c>trade_time</c> as
    /// <c>YYYYMMDD-hh:mm:ss</c>; see <see cref="TradeTimeUtc"/>.
    /// </remarks>
    [JsonPropertyName("trade_time_r")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? TradeTime { get; init; }

    /// <summary>When the execution occurred, from IBKR's <c>YYYYMMDD-hh:mm:ss</c> field.</summary>
    [JsonPropertyName("trade_time")]
    [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
    public Instant? TradeTimeUtc { get; init; }

    /// <summary>The size of the execution.</summary>
    [JsonPropertyName("size")]
    public decimal? Size { get; init; }

    /// <summary>The price the execution occurred at.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>The net amount of the execution.</summary>
    [JsonPropertyName("net_amount")]
    public decimal? NetAmount { get; init; }

    /// <summary>Commissions incurred.</summary>
    [JsonPropertyName("commission")]
    public decimal? Commission { get; init; }

    /// <summary>The venue the execution occurred on.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The instrument's listing exchange.</summary>
    [JsonPropertyName("listing_exchange")]
    public string? ListingExchange { get; init; }

    /// <summary>The account that received the execution.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The client-supplied order reference.</summary>
    [JsonPropertyName("order_ref")]
    public string? OrderReference { get; init; }

    /// <summary>The IBKR username that originated the order.</summary>
    [JsonPropertyName("submitter")]
    public string? Submitter { get; init; }

    /// <summary>The firm clearing the trade.</summary>
    [JsonPropertyName("clearing_name")]
    public string? ClearingName { get; init; }

    /// <summary>The identifier of the clearing firm.</summary>
    [JsonPropertyName("clearing_id")]
    public string? ClearingId { get; init; }

    /// <summary>Whether the trade resulted from a liquidation by IBKR.</summary>
    [JsonPropertyName("liquidation_trade")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsLiquidation { get; init; }

    /// <summary>Whether the order was an event trading order.</summary>
    [JsonPropertyName("is_event_trading")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsEventTrading { get; init; }

    /// <summary>Whether IBKR's tax optimizer supports the order.</summary>
    [JsonPropertyName("supports_tax_opt")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? SupportsTaxOptimizer { get; init; }
}
