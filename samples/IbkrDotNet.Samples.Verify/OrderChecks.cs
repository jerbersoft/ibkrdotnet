using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The order write path, exercised with an order that cannot fill.
/// </summary>
/// <remarks>
/// <para>
/// Opt-in, and refused outside a paper account. The order is a limit buy priced a quarter below the
/// market with a quantity of one, so it rests rather than executing, and it is cancelled in a
/// <c>finally</c> block whether the checks pass or throw. The sweep then asks IBKR to confirm the
/// account is flat, rather than assuming the cancel worked.
/// </para>
/// <para>
/// Nothing here fills, so <c>Execution</c> and the trade-time encodings are out of reach. Those
/// belong to <see cref="ExecutionChecks"/>, behind a separate flag: an order that cannot execute
/// and an order that is meant to are different enough to ask for separately.
/// </para>
/// </remarks>
internal static class OrderChecks
{
    /// <summary>How far below the market the resting order sits.</summary>
    private const decimal UnfillableDiscount = 0.75m;

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        ReadOnlyChecks.Context context,
        CancellationToken cancellationToken)
    {
        probe.Group("Orders (write path)");

        if (context.ReferencePrice is not { } reference || reference <= 0)
        {
            probe.Skip("POST /iserver/account/{a}/orders", "no quote, so no price known to be unfillable");
            return;
        }

        var limit = Math.Round(reference * UnfillableDiscount, 2);
        Console.WriteLine($"  resting BUY 1 @ {limit} against a market of {reference}");

        OrderId? placed = null;
        string? replied = null;
        try
        {
            await probe.RunAsync("POST /iserver/account/{a}/orders", async () =>
            {
                var result = await ibkr.Orders.SubmitAsync(
                    account,
                    [Ticket(context.ConId, quantity: 1, limit)],
                    OrderReplyPolicy.Manual,
                    cancellationToken);

                // A reply message is a question, not a rejection: IBKR wants a precautionary limit
                // confirmed before the order goes to work. Each reply id is answered exactly once.
                while (result is OrderSubmissionResult.ReplyRequired reply)
                {
                    var question = string.Join(" | ", reply.Messages.SelectMany(m => m.Message));
                    result = await ibkr.Orders.ConfirmReplyAsync(
                        reply.Messages[0].Id!, confirmed: true, cancellationToken);

                    replied = $"{Describe(result)} after \"{question}\"";
                }

                if (result is OrderSubmissionResult.Accepted accepted)
                {
                    placed = accepted.Orders[0].OrderId;
                }

                return Describe(result);
            });

            if (replied is null)
            {
                probe.Skip(
                    "POST /iserver/reply/{id}",
                    "no precautionary limit on this username, so IBKR asked nothing to confirm");
            }
            else
            {
                probe.Pass("POST /iserver/reply/{id}", replied);
            }

            if (placed is not { } orderId)
            {
                probe.Skip("GET  /iserver/account/order/status/{id}", "nothing was accepted to ask about");
                return;
            }

            await probe.RunAsync("GET  /iserver/account/order/status/{id}", async () =>
            {
                var status = await ibkr.Orders.GetOrderStatusAsync(orderId, cancellationToken);
                return $"{status.Status} ({status.StatusDescription}) {status.Side} {status.Size} {status.Symbol}";
            });

            await probe.RunAsync("POST /iserver/account/{a}/order/{id}", async () =>
            {
                // A modification repeats the whole ticket, not only the fields that change.
                var modified = await ibkr.Orders.ModifyAsync(
                    account,
                    orderId,
                    Ticket(context.ConId, quantity: 2, Math.Round(limit - 1m, 2)),
                    OrderReplyPolicy.Manual,
                    cancellationToken);

                if (modified is OrderSubmissionResult.ReplyRequired reply)
                {
                    modified = await ibkr.Orders.ConfirmReplyAsync(
                        reply.Messages[0].Id!, confirmed: true, cancellationToken);
                }

                var after = await ibkr.Orders.GetOrderStatusAsync(orderId, cancellationToken);
                return $"{Describe(modified)}, now size {after.Size}";
            });

            await probe.RunAsync("POST /iserver/questions/suppress", async () =>
                $"{(await ibkr.Orders.SuppressMessagesAsync(["o163"], cancellationToken)).Status}");

            await probe.RunAsync("POST /iserver/questions/suppress/reset", async () =>
                $"{(await ibkr.Orders.ResetMessageSuppressionAsync(cancellationToken)).Status}");
        }
        finally
        {
            if (placed is { } toCancel)
            {
                await probe.RunAsync("DELETE /iserver/account/{a}/order/{id}", async () =>
                {
                    var cancelled = await ibkr.Orders.CancelAsync(account, toCancel, cancellationToken: cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                    // Asked rather than assumed: the point of the check is that the account is flat.
                    var after = await ibkr.Orders.GetOrderStatusAsync(toCancel, cancellationToken);
                    return $"{cancelled.Message}; status is now {after.Status}";
                });
            }
        }
    }

    private static OrderTicket Ticket(ConId conId, decimal quantity, decimal limit) =>
        new()
        {
            ConId = conId.Value,
            OrderType = "LMT",
            Side = OrderSide.Buy,
            TimeInForce = TimeInForce.Day,
            Quantity = quantity,
            Price = limit,
        };

    internal static string Describe(OrderSubmissionResult result) => result switch
    {
        OrderSubmissionResult.Accepted accepted =>
            $"Accepted {accepted.Orders[0].OrderId} ({accepted.Orders[0].OrderStatus})",
        OrderSubmissionResult.ReplyRequired reply =>
            $"ReplyRequired [{string.Join(",", reply.Messages.SelectMany(m => m.MessageIds))}]",
        OrderSubmissionResult.Rejected rejected => $"Rejected: {rejected.Reject.Text}",
        OrderSubmissionResult.Failed failed => $"Failed: {failed.Error}",
        _ => result.GetType().Name,
    };
}
