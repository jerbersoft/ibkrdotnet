using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>The side of an order.</summary>
public enum OrderSide
{
    /// <summary>Buy.</summary>
    [JsonStringEnumMemberName("BUY")]
    Buy,

    /// <summary>Sell.</summary>
    [JsonStringEnumMemberName("SELL")]
    Sell,
}

/// <summary>How long an order remains working.</summary>
public enum TimeInForce
{
    /// <summary>Valid for the current trading day.</summary>
    [JsonStringEnumMemberName("DAY")]
    Day,

    /// <summary>Immediate or cancel.</summary>
    [JsonStringEnumMemberName("IOC")]
    ImmediateOrCancel,

    /// <summary>Good until cancelled.</summary>
    [JsonStringEnumMemberName("GTC")]
    GoodTillCancelled,

    /// <summary>At the opening.</summary>
    [JsonStringEnumMemberName("OPG")]
    AtTheOpening,

    /// <summary>Price and size improvement auction.</summary>
    [JsonStringEnumMemberName("PAX")]
    PriceImprovementAuction,
}

/// <summary>How a trailing order's offset is expressed.</summary>
public enum TrailingType
{
    /// <summary>An absolute amount.</summary>
    [JsonStringEnumMemberName("amt")]
    Amount,

    /// <summary>A percentage.</summary>
    [JsonStringEnumMemberName("%")]
    Percent,
}

/// <summary>
/// An order ticket, as submitted to <c>POST /iserver/account/{accountId}/orders</c>,
/// <c>.../orders/whatif</c> and <c>.../order/{orderId}</c>.
/// </summary>
/// <remarks>
/// A modification request must repeat every instruction of the original ticket, not only the fields
/// being changed.
/// </remarks>
public sealed record OrderTicket
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public required long ConId { get; init; }

    /// <summary>The IBKR order type identifier, for example <c>LMT</c> or <c>MKT</c>.</summary>
    [JsonPropertyName("orderType")]
    public required string OrderType { get; init; }

    /// <summary>The side of the order.</summary>
    [JsonPropertyName("side")]
    public required OrderSide Side { get; init; }

    /// <summary>How long the order remains working.</summary>
    [JsonPropertyName("tif")]
    public required TimeInForce TimeInForce { get; init; }

    /// <summary>The quantity, in units of the instrument.</summary>
    [JsonPropertyName("quantity")]
    public required decimal Quantity { get; init; }

    /// <summary>The receiving account.</summary>
    [JsonPropertyName("acctId")]
    public string? AccountId { get; init; }

    /// <summary>
    /// The contract identifier and routing destination together, as <c>123456@EXCHANGE</c>.
    /// </summary>
    /// <remarks>
    /// A combination or spread order sets this instead of <see cref="ConId"/>, encoding the legs.
    /// </remarks>
    [JsonPropertyName("conidex")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>The IBKR asset class identifier.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>
    /// A client-supplied order identifier, unique over any 24-hour span and at most 64 characters.
    /// </summary>
    /// <remarks>Do not set this on the child of a bracket.</remarks>
    [JsonPropertyName("cOID")]
    public string? ClientOrderId { get; init; }

    /// <summary>
    /// The parent's <see cref="ClientOrderId"/>, for the child leg of a bracket.
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }

    /// <summary>The instrument's listing exchange.</summary>
    [JsonPropertyName("listingExchange")]
    public string? ListingExchange { get; init; }

    /// <summary>
    /// Whether every order in the containing array forms a one-cancels-all group.
    /// </summary>
    [JsonPropertyName("isSingleGroup")]
    public bool? IsSingleGroup { get; init; }

    /// <summary>Whether the order may execute outside regular trading hours.</summary>
    [JsonPropertyName("outsideRTH")]
    public bool? OutsideRegularTradingHours { get; init; }

    /// <summary>Whether the order must fill entirely or not at all.</summary>
    [JsonPropertyName("allOrNone")]
    public bool? AllOrNone { get; init; }

    /// <summary>The order's price, where the order type uses one.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>An additional price, used by stop orders among others.</summary>
    [JsonPropertyName("auxPrice")]
    public decimal? AuxPrice { get; init; }

    /// <summary>The instrument's ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The offset used by a trailing order.</summary>
    [JsonPropertyName("trailingAmt")]
    public decimal? TrailingAmount { get; init; }

    /// <summary>How <see cref="TrailingAmount"/> is expressed.</summary>
    [JsonPropertyName("trailingType")]
    public TrailingType? TrailingType { get; init; }

    /// <summary>The quantity expressed as an amount of currency, for cash quantity orders.</summary>
    [JsonPropertyName("cashQty")]
    public decimal? CashQuantity { get; init; }

    /// <summary>Whether to apply IBKR's Price Management Algo.</summary>
    [JsonPropertyName("useAdaptive")]
    public bool? UseAdaptive { get; init; }

    /// <summary>
    /// Whether a forex order is a currency conversion rather than a position.
    /// </summary>
    [JsonPropertyName("isCcyConv")]
    public bool? IsCurrencyConversion { get; init; }

    /// <summary>The name of an execution algorithm.</summary>
    [JsonPropertyName("strategy")]
    public string? Strategy { get; init; }

    /// <summary>Parameters for the selected execution algorithm.</summary>
    [JsonPropertyName("strategyParameters")]
    public IReadOnlyDictionary<string, object>? StrategyParameters { get; init; }

    /// <summary>An identifier for an external operator.</summary>
    [JsonPropertyName("extOperator")]
    public string? ExternalOperator { get; init; }

    /// <summary>
    /// Whether the order was originated by a person rather than by an automated system.
    /// </summary>
    /// <remarks>
    /// Required on every US futures order. IBKR rejects orders for USFUT products that omit it.
    /// </remarks>
    [JsonPropertyName("manualIndicator")]
    public bool? ManualIndicator { get; init; }

    /// <summary>The tax lot selection to apply, for gains and losses management.</summary>
    [JsonPropertyName("taxOptimizerId")]
    public string? TaxOptimizerId { get; init; }

    /// <summary>The IBKR user interface element the order originated from.</summary>
    [JsonPropertyName("referrer")]
    public string? Referrer { get; init; }

    /// <summary>Allocation instructions, for advisor orders spread across subaccounts.</summary>
    [JsonPropertyName("jsonPayload")]
    public OrderJsonPayload? JsonPayload { get; init; }
}

/// <summary>Additional structured instructions attached to an order ticket.</summary>
public sealed record OrderJsonPayload
{
    /// <summary>How the order should be allocated across subaccounts.</summary>
    [JsonPropertyName("allocation_profile")]
    public AllocationProfile? AllocationProfile { get; init; }
}

/// <summary>How an order should be allocated across subaccounts.</summary>
public sealed record AllocationProfile
{
    /// <summary>Whether allocation amounts are share counts or cash amounts.</summary>
    [JsonPropertyName("alloc_type")]
    public AllocationType? AllocationType { get; init; }

    /// <summary>The accounts to allocate to, and how much each receives.</summary>
    [JsonPropertyName("allocations")]
    public IReadOnlyList<AccountAllocation> Allocations { get; init; } = [];
}

/// <summary>How allocation amounts should be read.</summary>
public enum AllocationType
{
    /// <summary>Amounts are quantities of shares.</summary>
    [JsonStringEnumMemberName("SHARE")]
    Share,

    /// <summary>Amounts are cash quantities.</summary>
    [JsonStringEnumMemberName("CASH")]
    Cash,
}

/// <summary>One account's share of an allocated order.</summary>
public sealed record AccountAllocation
{
    /// <summary>The receiving account.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The amount, read according to the profile's allocation type.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }
}

/// <summary>The body of an order submission or preview request.</summary>
/// <param name="Orders">
/// The order tickets. IBKR accepts one ticket per request unless the array forms a bracket or a
/// one-cancels-all group.
/// </param>
internal sealed record OrderSubmissionRequest(
    [property: JsonPropertyName("orders")] IReadOnlyList<OrderTicket> Orders);
