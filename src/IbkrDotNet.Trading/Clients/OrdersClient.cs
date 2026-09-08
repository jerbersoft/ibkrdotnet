using System.Globalization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IOrdersClient" />
public sealed class OrdersClient(IIbkrApiClient apiClient) : IOrdersClient
{
    /// <summary>
    /// How many reply messages will be confirmed in sequence under
    /// <see cref="OrderReplyPolicy.AutoConfirm"/> before the client gives up.
    /// </summary>
    /// <remarks>
    /// Confirming one message can produce another. The bound exists so a server that keeps asking
    /// cannot turn auto-confirmation into an unbounded loop of order-affecting requests.
    /// </remarks>
    public const int MaxAutomaticReplyConfirmations = 10;

    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<OrderSubmissionResult> SubmitAsync(
        AccountId accountId,
        IReadOnlyList<OrderTicket> orders,
        OrderReplyPolicy replyPolicy = OrderReplyPolicy.Manual,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orders);
        if (orders.Count == 0)
        {
            throw new ArgumentException("At least one order ticket is required.", nameof(orders));
        }

        var request = IbkrRequest
            .Post($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/orders")
            .WithJsonBody(new OrderSubmissionRequest(orders));

        return SubmitCoreAsync(request, replyPolicy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OrderPreview> PreviewAsync(
        AccountId accountId,
        IReadOnlyList<OrderTicket> orders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orders);
        if (orders.Count == 0)
        {
            throw new ArgumentException("At least one order ticket is required.", nameof(orders));
        }

        return _apiClient.SendAsync<OrderPreview>(
            IbkrRequest
                .Post($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/orders/whatif")
                .WithJsonBody(new OrderSubmissionRequest(orders)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<OrderSubmissionResult> ModifyAsync(
        AccountId accountId,
        OrderId orderId,
        OrderTicket order,
        OrderReplyPolicy replyPolicy = OrderReplyPolicy.Manual,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // Modification takes a single object, not the array submission uses.
        var request = IbkrRequest
            .Post($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}" +
                  $"/order/{IbkrRequest.PathSegment(orderId.Value)}")
            .WithJsonBody(order);

        return SubmitCoreAsync(request, replyPolicy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CancelOrderResponse> CancelAsync(
        AccountId accountId,
        OrderId orderId,
        bool? manualIndicator = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<CancelOrderResponse>(
            IbkrRequest
                .Delete($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}" +
                        $"/order/{IbkrRequest.PathSegment(orderId.Value)}")
                .WithQuery("manualIndicator", manualIndicator),
            cancellationToken);

    /// <inheritdoc />
    public Task<OrderSubmissionResult> ConfirmReplyAsync(
        string replyId,
        bool confirmed = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyId);
        return SubmitCoreAsync(
            IbkrRequest
                .Post($"/v1/api/iserver/reply/{IbkrRequest.PathSegment(replyId)}")
                .WithJsonBody(new OrderReplyRequest(confirmed)),
            OrderReplyPolicy.Manual,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<LiveOrders> GetOpenOrdersAsync(
        IEnumerable<string>? filters = null,
        bool? force = null,
        AccountId? accountId = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<LiveOrders>(
            IbkrRequest.Get("/v1/api/iserver/account/orders")
                .WithCommaSeparatedQuery("filters", filters)
                .WithQuery("force", force)
                .WithQuery("accountId", accountId?.Value),
            cancellationToken);

    /// <inheritdoc />
    public Task<OrderStatus> GetOrderStatusAsync(
        OrderId orderId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<OrderStatus>(
            IbkrRequest.Get($"/v1/api/iserver/account/order/status/{IbkrRequest.PathSegment(orderId.Value)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Execution>> GetTradesAsync(
        int? days = null,
        AccountId? accountId = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<Execution>>(
            IbkrRequest.Get("/v1/api/iserver/account/trades")
                .WithQuery("days", days?.ToString(CultureInfo.InvariantCulture))
                .WithQuery("accountId", accountId?.Value),
            cancellationToken);

    /// <inheritdoc />
    public Task<SuppressionStatus> SuppressMessagesAsync(
        IEnumerable<string> messageIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        return _apiClient.SendAsync<SuppressionStatus>(
            IbkrRequest.Post("/v1/api/iserver/questions/suppress")
                .WithJsonBody(new SuppressMessagesRequest([.. messageIds])),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<SuppressionStatus> ResetMessageSuppressionAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<SuppressionStatus>(
            IbkrRequest.Post("/v1/api/iserver/questions/suppress/reset"),
            cancellationToken);

    /// <inheritdoc />
    public Task DismissServerPromptAsync(
        long orderId,
        string requestId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return _apiClient.SendAsync(
            IbkrRequest.Post("/v1/api/iserver/notification")
                .WithJsonBody(new ServerPromptResponse(orderId, requestId, text)),
            cancellationToken);
    }

    private async Task<OrderSubmissionResult> SubmitCoreAsync(
        IbkrRequest request,
        OrderReplyPolicy replyPolicy,
        CancellationToken cancellationToken)
    {
        var result = await SendAndParseAsync(request, cancellationToken).ConfigureAwait(false);

        if (replyPolicy != OrderReplyPolicy.AutoConfirm)
        {
            return result;
        }

        var confirmations = 0;
        while (result is OrderSubmissionResult.ReplyRequired reply)
        {
            var message = reply.Messages.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Id));
            if (message?.Id is not { Length: > 0 } replyId)
            {
                return result;
            }

            if (++confirmations > MaxAutomaticReplyConfirmations)
            {
                throw new IbkrApiException(
                    $"IBKR asked for confirmation more than {MaxAutomaticReplyConfirmations} times " +
                    "while auto-confirming an order. The client stopped rather than continuing to " +
                    "send order-affecting requests.")
                {
                    Method = request.Method.Method,
                    Path = request.Path,
                };
            }

            result = await SendAndParseAsync(
                    IbkrRequest
                        .Post($"/v1/api/iserver/reply/{IbkrRequest.PathSegment(replyId)}")
                        .WithJsonBody(new OrderReplyRequest(true)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private async Task<OrderSubmissionResult> SendAndParseAsync(
        IbkrRequest request,
        CancellationToken cancellationToken)
    {
        // The response is one of four unrelated shapes, so it is read as raw JSON and discriminated
        // rather than deserialized into a single type.
        var body = await _apiClient.SendAsync<System.Text.Json.JsonElement>(request, cancellationToken)
            .ConfigureAwait(false);

        return OrderSubmissionResultParser.Parse(body.GetRawText());
    }
}
