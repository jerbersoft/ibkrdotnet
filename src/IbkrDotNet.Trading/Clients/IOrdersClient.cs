using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>The Trading Orders endpoints: placing, previewing, amending and tracking orders.</summary>
public interface IOrdersClient
{
    /// <summary>
    /// Submits one or more order tickets.
    /// </summary>
    /// <param name="accountId">The account the order clears to.</param>
    /// <param name="orders">
    /// The tickets. IBKR accepts one per request unless the array forms a bracket or a
    /// one-cancels-all group.
    /// </param>
    /// <param name="replyPolicy">
    /// How to treat a reply message IBKR asks the caller to confirm. Defaults to
    /// <see cref="OrderReplyPolicy.Manual"/>, which returns the messages rather than accepting them.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// One of four outcomes. A <see cref="OrderSubmissionResult.ReplyRequired"/> is not a rejection:
    /// IBKR wants something about the ticket confirmed before the order goes to work.
    /// </returns>
    Task<OrderSubmissionResult> SubmitAsync(
        AccountId accountId,
        IReadOnlyList<OrderTicket> orders,
        OrderReplyPolicy replyPolicy = OrderReplyPolicy.Manual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews an order's effect on margin and equity without submitting it.
    /// </summary>
    /// <param name="accountId">The account the order would clear to.</param>
    /// <param name="orders">The tickets to preview.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Some instruments set <c>forceOrderPreview</c> in their market rules, in which case submitting
    /// without previewing first is rejected.
    /// </remarks>
    Task<OrderPreview> PreviewAsync(
        AccountId accountId,
        IReadOnlyList<OrderTicket> orders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies a working order.
    /// </summary>
    /// <param name="accountId">The account the order belongs to.</param>
    /// <param name="orderId">The order to modify.</param>
    /// <param name="order">
    /// The complete replacement ticket. Every instruction from the original must be repeated, not
    /// only the fields being changed.
    /// </param>
    /// <param name="replyPolicy">How to treat a reply message IBKR asks the caller to confirm.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Modification is governed by a different ruleset from submission; inspect it with
    /// <see cref="IContractsClient.GetRulesAsync"/> passing <c>modifyOrder</c>.
    /// </remarks>
    Task<OrderSubmissionResult> ModifyAsync(
        AccountId accountId,
        OrderId orderId,
        OrderTicket order,
        OrderReplyPolicy replyPolicy = OrderReplyPolicy.Manual,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels a working order.</summary>
    /// <param name="accountId">The account the order belongs to.</param>
    /// <param name="orderId">The order to cancel.</param>
    /// <param name="manualIndicator">
    /// Whether the cancellation was originated by a person. Required for US futures orders.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<CancelOrderResponse> CancelAsync(
        AccountId accountId,
        OrderId orderId,
        bool? manualIndicator = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms an order reply message so the order can go to work.
    /// </summary>
    /// <param name="replyId">The reply identifier from the submission response.</param>
    /// <param name="confirmed">Whether to proceed with the order.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<OrderSubmissionResult> ConfirmReplyAsync(
        string replyId,
        bool confirmed = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists orders that are working, or were filled or cancelled during this brokerage session.
    /// </summary>
    /// <param name="filters">Order statuses to filter by, or <c>sort_by_time</c> to sort.</param>
    /// <param name="force">
    /// Whether to clear IBKR's cache and refresh from the brokerage backend. The response to a
    /// forced request is an empty array; read the orders on the request that follows.
    /// </param>
    /// <param name="accountId">The account to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>Limited by IBKR to one request every five seconds.</remarks>
    Task<LiveOrders> GetOpenOrdersAsync(
        IEnumerable<string>? filters = null,
        bool? force = null,
        AccountId? accountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the status of one order.</summary>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<OrderStatus> GetOrderStatusAsync(OrderId orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists executions from the current day, and optionally from prior days.
    /// </summary>
    /// <param name="days">Prior days to include, up to seven. Omit for the current day only.</param>
    /// <param name="accountId">The account to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>Limited by IBKR to one request every five seconds.</remarks>
    Task<IReadOnlyList<Execution>> GetTradesAsync(
        int? days = null,
        AccountId? accountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suppresses categories of order reply message for the rest of the brokerage session.
    /// </summary>
    /// <param name="messageIds">The message categories, for example <c>o163</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// IBKR recommends sending the full list once at the start of a session, before trading. Adding
    /// one later means resending the complete array, not just the new entry. A message category does
    /// not have to have been seen before it can be suppressed.
    /// </remarks>
    Task<SuppressionStatus> SuppressMessagesAsync(
        IEnumerable<string> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>Restores delivery of every suppressed order reply message.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SuppressionStatus> ResetMessageSuppressionAsync(CancellationToken cancellationToken = default);

    /// <summary>Answers a server prompt.</summary>
    /// <param name="orderId">The order the prompt relates to.</param>
    /// <param name="requestId">IBKR's request identifier from the prompt.</param>
    /// <param name="text">The chosen value from the prompt's options.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task DismissServerPromptAsync(
        long orderId,
        string requestId,
        string text,
        CancellationToken cancellationToken = default);
}
