using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>The response from <c>GET /iserver/account/orders</c>.</summary>
public sealed record LiveOrders
{
    /// <summary>The orders.</summary>
    [JsonPropertyName("orders")]
    public IReadOnlyList<LiveOrder> Orders { get; init; } = [];

    /// <summary>Whether the response is a snapshot.</summary>
    [JsonPropertyName("snapshot")]
    public bool? Snapshot { get; init; }
}

/// <summary>
/// An order that is working, or that was filled or cancelled during this brokerage session.
/// </summary>
/// <remarks>
/// IBKR returns most numeric fields here as strings, and expresses colours and display strings
/// alongside the trading data because the same payload drives Client Portal's order list.
/// </remarks>
public sealed record LiveOrder
{
    /// <summary>The IBKR-assigned order identifier.</summary>
    [JsonPropertyName("orderId")]
    public OrderId OrderId { get; init; }

    /// <summary>The account the order clears to.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The account the order was entered under.</summary>
    [JsonPropertyName("acct")]
    public string? EnteringAccount { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier and routing destination, as <c>123456@EXCHANGE</c>.</summary>
    [JsonPropertyName("conidex")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The instrument's description.</summary>
    [JsonPropertyName("description1")]
    public string? Description { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>The exchange the order routed to.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The instrument's listing exchange.</summary>
    [JsonPropertyName("listingExchange")]
    public string? ListingExchange { get; init; }

    /// <summary>The side of the order.</summary>
    [JsonPropertyName("side")]
    public string? Side { get; init; }

    /// <summary>The order's status, for example <c>Filled</c> or <c>Submitted</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The order's status at the central counterparty.</summary>
    [JsonPropertyName("order_ccp_status")]
    public string? ClearingStatus { get; init; }

    /// <summary>A human-readable description of the order.</summary>
    [JsonPropertyName("orderDesc")]
    public string? OrderDescription { get; init; }

    /// <summary>The order type, as displayed.</summary>
    [JsonPropertyName("orderType")]
    public string? OrderType { get; init; }

    /// <summary>The order type as originally submitted.</summary>
    [JsonPropertyName("origOrderType")]
    public string? OriginalOrderType { get; init; }

    /// <summary>The time in force.</summary>
    [JsonPropertyName("timeInForce")]
    public string? TimeInForce { get; init; }

    /// <summary>The order's price.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>The average fill price.</summary>
    [JsonPropertyName("avgPrice")]
    public decimal? AveragePrice { get; init; }

    /// <summary>The total order size.</summary>
    [JsonPropertyName("totalSize")]
    public decimal? TotalSize { get; init; }

    /// <summary>The quantity filled so far.</summary>
    [JsonPropertyName("filledQuantity")]
    public decimal? FilledQuantity { get; init; }

    /// <summary>The quantity still working.</summary>
    [JsonPropertyName("remainingQuantity")]
    public decimal? RemainingQuantity { get; init; }

    /// <summary>The cash size, for cash quantity orders.</summary>
    [JsonPropertyName("totalCashSize")]
    public decimal? TotalCashSize { get; init; }

    /// <summary>The currency cash quantities are expressed in.</summary>
    [JsonPropertyName("cashCcy")]
    public string? CashCurrency { get; init; }

    /// <summary>Size and fills, as displayed.</summary>
    [JsonPropertyName("sizeAndFills")]
    public string? SizeAndFills { get; init; }

    /// <summary>
    /// The time of the last execution against the order.
    /// </summary>
    /// <remarks>
    /// Read from <c>lastExecutionTime_r</c>, which is epoch milliseconds in a JSON string. IBKR also
    /// sends <c>lastExecutionTime</c> in <c>YYMMDDhhmmss</c>, whose two-digit year makes it the
    /// weaker source; see <see cref="LastExecutionTimeCompact"/>.
    /// </remarks>
    [JsonPropertyName("lastExecutionTime_r")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? LastExecutionTime { get; init; }

    /// <summary>The time of the last execution, from IBKR's <c>YYMMDDhhmmss</c> field.</summary>
    [JsonPropertyName("lastExecutionTime")]
    [JsonConverter(typeof(IbkrCompactDateTimeConverter))]
    public Instant? LastExecutionTimeCompact { get; init; }

    /// <summary>Whether the order is an event trading order.</summary>
    [JsonPropertyName("isEventTrading")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsEventTrading { get; init; }

    /// <summary>Whether IBKR's tax optimizer supports the order.</summary>
    [JsonPropertyName("supportsTaxOpt")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? SupportsTaxOptimizer { get; init; }

    /// <summary>The tax lot selection applied.</summary>
    [JsonPropertyName("taxOptimizerId")]
    public string? TaxOptimizerId { get; init; }

    /// <summary>The client-supplied order reference, when one was set.</summary>
    [JsonPropertyName("order_ref")]
    public string? OrderReference { get; init; }

    /// <summary>The parent order identifier, for the child leg of a bracket.</summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }
}
