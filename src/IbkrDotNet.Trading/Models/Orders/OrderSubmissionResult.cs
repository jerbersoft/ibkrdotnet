using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>
/// The outcome of submitting or modifying an order.
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /iserver/account/{accountId}/orders</c> answers with one of four unrelated shapes: an
/// acknowledgement, an array of reply messages awaiting confirmation, an advanced rejection, or a
/// bare error. Returning a single flattened type would leave every caller inspecting which fields
/// happened to be populated, so the outcomes are distinct types.
/// </para>
/// <para>
/// A <see cref="ReplyRequired"/> result is not a rejection. IBKR is asking the caller to confirm
/// something about the ticket — most often a precautionary limit configured on the username — and
/// the order goes to work once the message is confirmed.
/// </para>
/// </remarks>
public abstract record OrderSubmissionResult
{
    private OrderSubmissionResult()
    {
    }

    /// <summary>IBKR accepted the order.</summary>
    /// <param name="Orders">The acknowledgements, one per ticket submitted.</param>
    public sealed record Accepted(IReadOnlyList<OrderConfirmation> Orders) : OrderSubmissionResult;

    /// <summary>
    /// IBKR needs the caller to confirm something before the order can go to work.
    /// </summary>
    /// <param name="Messages">The messages awaiting confirmation.</param>
    public sealed record ReplyRequired(IReadOnlyList<OrderReplyMessage> Messages) : OrderSubmissionResult;

    /// <summary>IBKR rejected the order and offered a set of choices in response.</summary>
    /// <param name="Reject">The rejection.</param>
    public sealed record Rejected(AdvancedOrderReject Reject) : OrderSubmissionResult;

    /// <summary>IBKR reported that submission was not successful.</summary>
    /// <param name="Error">The error message.</param>
    public sealed record Failed(string Error) : OrderSubmissionResult;
}

/// <summary>An acknowledgement that IBKR accepted an order ticket.</summary>
public sealed record OrderConfirmation
{
    /// <summary>The order identifier IBKR assigned.</summary>
    [JsonPropertyName("order_id")]
    public OrderId OrderId { get; init; }

    /// <summary>The order's status at the moment of acknowledgement.</summary>
    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; init; }

    /// <summary>Internal use.</summary>
    [JsonPropertyName("encrypt_message")]
    public string? EncryptMessage { get; init; }

    /// <summary>The client-supplied order identifier, when one was set.</summary>
    [JsonPropertyName("local_order_id")]
    public string? LocalOrderId { get; init; }

    /// <summary>Any accompanying text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
/// A message IBKR requires the caller to confirm before an order will go to work.
/// </summary>
/// <remarks>
/// These are typically precautionary checks configured on the username — effectively fat-finger
/// protections. Confirm one with <c>POST /iserver/reply/{replyId}</c>, or suppress its category for
/// the rest of the brokerage session using <see cref="MessageIds"/>.
/// </remarks>
public sealed record OrderReplyMessage
{
    /// <summary>The reply identifier, passed to <c>/iserver/reply/{replyId}</c> to confirm.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The human-readable text of the message.</summary>
    [JsonPropertyName("message")]
    public IReadOnlyList<string> Message { get; init; } = [];

    /// <summary>Whether this category of message is currently suppressed.</summary>
    [JsonPropertyName("isSuppressed")]
    public bool? IsSuppressed { get; init; }

    /// <summary>
    /// The identifiers categorizing this message, for example <c>o163</c>.
    /// </summary>
    /// <remarks>
    /// Pass these to <c>POST /iserver/questions/suppress</c> to stop being asked for the rest of the
    /// brokerage session.
    /// </remarks>
    [JsonPropertyName("messageIds")]
    public IReadOnlyList<string> MessageIds { get; init; } = [];
}

/// <summary>A rejection that offers the caller a set of choices in response.</summary>
public sealed record AdvancedOrderReject
{
    /// <summary>The order identifier IBKR assigned to the rejected ticket.</summary>
    [JsonPropertyName("orderId")]
    public OrderId? OrderId { get; init; }

    /// <summary>IBKR's internal identifier for the message.</summary>
    [JsonPropertyName("reqId")]
    public string? RequestId { get; init; }

    /// <summary>The human-readable rejection text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The choices available in response.</summary>
    [JsonPropertyName("options")]
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>The message type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>IBKR's identifier for the category of message.</summary>
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    /// <summary>
    /// Whether the message is a prompt whose options may permit the order to be resubmitted.
    /// </summary>
    [JsonPropertyName("prompt")]
    public bool? Prompt { get; init; }

    /// <summary>Whether the prompt may be dismissed.</summary>
    [JsonPropertyName("dismissable")]
    public IReadOnlyList<string> Dismissable { get; init; } = [];
}

/// <summary>How the client should treat an <see cref="OrderSubmissionResult.ReplyRequired"/>.</summary>
public enum OrderReplyPolicy
{
    /// <summary>
    /// Return the messages to the caller, who decides whether to confirm. This is the default.
    /// </summary>
    /// <remarks>
    /// These prompts cover margin, liquidity and price-constraint warnings. Confirming one is a
    /// trading decision, so it is not made on the caller's behalf unless they ask for it.
    /// </remarks>
    Manual,

    /// <summary>
    /// Confirm each message automatically and return the resulting acknowledgement.
    /// </summary>
    /// <remarks>
    /// Only appropriate where the precautionary settings on the username are understood and the
    /// caller has decided in advance to accept every warning the order may raise.
    /// </remarks>
    AutoConfirm,
}

/// <summary>The body of <c>POST /iserver/reply/{replyId}</c>.</summary>
/// <param name="Confirmed">Whether to proceed with the order.</param>
internal sealed record OrderReplyRequest([property: JsonPropertyName("confirmed")] bool Confirmed);

/// <summary>The body of <c>POST /iserver/questions/suppress</c>.</summary>
/// <param name="MessageIds">The message categories to suppress for the brokerage session.</param>
internal sealed record SuppressMessagesRequest(
    [property: JsonPropertyName("messageIds")] IReadOnlyList<string> MessageIds);

/// <summary>The response from the order reply suppression endpoints.</summary>
public sealed record SuppressionStatus
{
    /// <summary>The outcome, for example <c>submitted</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>The response from cancelling an order.</summary>
public sealed record CancelOrderResponse
{
    /// <summary>The order that was cancelled.</summary>
    [JsonPropertyName("order_id")]
    public OrderId OrderId { get; init; }

    /// <summary>The outcome message.</summary>
    [JsonPropertyName("msg")]
    public string? Message { get; init; }

    /// <summary>The account the order belonged to.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public string? ConId { get; init; }
}

/// <summary>The body of <c>POST /iserver/notification</c>.</summary>
/// <param name="OrderId">The order the prompt relates to.</param>
/// <param name="RequestId">IBKR's request identifier from the prompt.</param>
/// <param name="Text">The chosen value from the prompt's options.</param>
public sealed record ServerPromptResponse(
    [property: JsonPropertyName("orderId")] long OrderId,
    [property: JsonPropertyName("reqId")] string RequestId,
    [property: JsonPropertyName("text")] string Text);
