using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>
/// The margin and equity impact of an order, from
/// <c>POST /iserver/account/{accountId}/orders/whatif</c>.
/// </summary>
/// <remarks>
/// Every value here is display text rather than a number — <c>"1,977.60 USD (10 Shares)"</c>,
/// <c>"123,455"</c> — because IBKR returns the strings its own interface shows. Some contracts set
/// <c>forceOrderPreview</c> in their market rules, in which case submitting without previewing first
/// is rejected.
/// </remarks>
public sealed record OrderPreview
{
    /// <summary>The order's cost, commission and total.</summary>
    [JsonPropertyName("amount")]
    public OrderPreviewAmount? Amount { get; init; }

    /// <summary>The effect on account equity.</summary>
    [JsonPropertyName("equity")]
    public OrderPreviewChange? Equity { get; init; }

    /// <summary>The effect on initial margin.</summary>
    [JsonPropertyName("initial")]
    public OrderPreviewChange? InitialMargin { get; init; }

    /// <summary>The effect on maintenance margin.</summary>
    [JsonPropertyName("maintenance")]
    public OrderPreviewChange? MaintenanceMargin { get; init; }

    /// <summary>The effect on the position.</summary>
    [JsonPropertyName("position")]
    public OrderPreviewChange? Position { get; init; }

    /// <summary>
    /// A warning IBKR attached to the preview, prefixed with its message code.
    /// </summary>
    /// <remarks>
    /// A warning here does not prevent submission, but the same condition will usually produce an
    /// order reply message that must be confirmed.
    /// </remarks>
    [JsonPropertyName("warn")]
    public string? Warning { get; init; }

    /// <summary>An error preventing the preview from being produced.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>The cost of a previewed order, as display text.</summary>
public sealed record OrderPreviewAmount
{
    /// <summary>The projected cost, for example <c>"1,977.60 USD (10 Shares)"</c>.</summary>
    [JsonPropertyName("amount")]
    public string? Amount { get; init; }

    /// <summary>The projected commission.</summary>
    [JsonPropertyName("commission")]
    public string? Commission { get; init; }

    /// <summary>The projected total.</summary>
    [JsonPropertyName("total")]
    public string? Total { get; init; }
}

/// <summary>A before-and-after value from an order preview, as display text.</summary>
public sealed record OrderPreviewChange
{
    /// <summary>The value before the order.</summary>
    [JsonPropertyName("current")]
    public string? Current { get; init; }

    /// <summary>The change the order would cause.</summary>
    [JsonPropertyName("change")]
    public string? Change { get; init; }

    /// <summary>The value after the order.</summary>
    [JsonPropertyName("after")]
    public string? After { get; init; }
}
