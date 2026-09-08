using System.Diagnostics;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;
using NodaTime;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The fill path: one share bought at market, then sold back.
/// </summary>
/// <remarks>
/// <para>
/// Opt-in under its own flag, separate from <see cref="OrderChecks"/>, and refused outside a paper
/// account. <see cref="OrderChecks"/> never fills by design, which leaves four wire formats
/// unreached: <c>trade_time</c>, <c>trade_time_r</c>, <c>lastExecutionTime</c> and
/// <c>lastExecutionTime_r</c>. Every fixture covering those was transcribed from IBKR's
/// documentation, so a converter reading them into the wrong instant would agree with its fixture
/// and still be wrong against the API. Only a real execution settles it.
/// </para>
/// <para>
/// This is the one place in the sweep that asserts on the data rather than only on deserialization.
/// IBKR sends each execution time twice -- once as epoch milliseconds, once as text in a zone the
/// docs assert but never demonstrate -- and the two decodings have to agree. A disagreement is a
/// converter bug, not a fact about the account, so it fails rather than printing a line.
/// </para>
/// <para>
/// The share is sold back in a <c>finally</c> block, and the sweep asks IBKR to confirm the position
/// is where it found it rather than assuming the sell worked. It restores the opening quantity
/// rather than flattening, because a paper account that has been used does not start flat. One share
/// of a liquid stock is the smallest thing that can produce an execution.
/// </para>
/// </remarks>
internal static class ExecutionChecks
{
    private const decimal Quantity = 1m;

    /// <summary>How long a market order gets to fill before the sweep gives up on it.</summary>
    private static readonly TimeSpan FillTimeout = TimeSpan.FromSeconds(45);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>How far two encodings of one timestamp may differ before it is a bug.</summary>
    /// <remarks>
    /// One second: the text formats carry whole seconds while the <c>_r</c> fields carry
    /// milliseconds, so a sub-second gap is truncation. Anything larger is a zone or a century.
    /// </remarks>
    private static readonly Duration EncodingTolerance = Duration.FromSeconds(1);

    /// <summary>Order statuses IBKR will not move off without another request.</summary>
    private static readonly string[] TerminalStatuses =
        ["Filled", "Cancelled", "Rejected", "Inactive"];

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        ReadOnlyChecks.Context context,
        CancellationToken cancellationToken)
    {
        probe.Group("Executions (filling order)");

        // Read where the account starts, because it need not start flat: a paper account that has
        // been used holds whatever was left in it. The checks below assert the position moved by one
        // and came back, which is the claim that holds either way.
        var opening = await ReadQuantityAsync(ibkr, account, context.ConId, cancellationToken);
        Console.WriteLine(
            $"  BUY {Quantity} at market on conid {context.ConId}, then SELL {Quantity} back; " +
            $"position starts at {opening}");

        OrderId? bought = null;
        var isLong = false;

        try
        {
            await probe.RunAsync("POST /iserver/account/{a}/orders (market)", async () =>
            {
                bought = await SubmitAsync(ibkr, account, context.ConId, OrderSide.Buy, cancellationToken);
                return $"accepted {bought}";
            });

            if (bought is not { } orderId)
            {
                probe.Skip("GET  /iserver/account/trades (execution)", "no order was accepted, so nothing can fill");
                return;
            }

            await probe.RunAsync("GET  /iserver/account/order/status/{id} (filled)", async () =>
            {
                var fill = await AwaitTerminalStatusAsync(ibkr, orderId, cancellationToken);
                if (!IsFilled(fill))
                {
                    throw new SkipCheckException($"the order ended {fill.Status}, not Filled");
                }

                isLong = true;
                return $"{fill.Status}: {fill.CumulativeFill} @ {fill.AveragePrice}, order_time {fill.OrderTime}";
            });

            if (!isLong)
            {
                probe.Skip("GET  /iserver/account/trades (execution)", "nothing filled, so there is no execution to read");
                return;
            }

            await probe.RunAsync("GET  /iserver/account/trades (execution)", async () =>
            {
                // IBKR reports a fill on the order before it reports the execution behind it.
                var execution = await AwaitExecutionAsync(ibkr, orderId, context.ConId, cancellationToken);

                Console.WriteLine($"      execution_id  {execution.ExecutionId}");
                Console.WriteLine($"      trade_time    {execution.TradeTimeUtc}  (YYYYMMDD-hh:mm:ss)");
                Console.WriteLine($"      trade_time_r  {execution.TradeTime}  (epoch ms)");

                var agreement = Compare(
                    "trade_time", execution.TradeTimeUtc,
                    "trade_time_r", execution.TradeTime);

                return $"{execution.Side} {execution.Size} @ {execution.Price} on {execution.Exchange}; {agreement}";
            });

            await probe.RunAsync("GET  /iserver/account/orders (executed)", async () =>
            {
                var orders = await ibkr.Orders.GetOpenOrdersAsync(cancellationToken: cancellationToken);
                var order = orders.Orders.FirstOrDefault(o => o.OrderId == orderId)
                    ?? throw new SkipCheckException($"IBKR did not list order {orderId}");

                Console.WriteLine($"      lastExecutionTime    {order.LastExecutionTimeCompact}  (YYMMDDhhmmss)");
                Console.WriteLine($"      lastExecutionTime_r  {order.LastExecutionTime}  (epoch ms)");

                var agreement = Compare(
                    "lastExecutionTime", order.LastExecutionTimeCompact,
                    "lastExecutionTime_r", order.LastExecutionTime);

                return $"{order.Status} {order.FilledQuantity}/{order.TotalSize} @ {order.AveragePrice}; {agreement}";
            });

            await probe.RunAsync("GET  /portfolio/{a}/position/{c} (held)", async () =>
            {
                var expected = opening + Quantity;
                var quantity = await AwaitQuantityAsync(ibkr, account, context.ConId, expected, cancellationToken);
                return quantity == expected
                    ? $"position went {opening} -> {quantity}"
                    : $"expected {expected} after the fill, IBKR reports {quantity}";
            });
        }
        finally
        {
            await CleanUpAsync(ibkr, probe, account, context.ConId, bought, isLong, opening, cancellationToken);
        }
    }

    /// <summary>
    /// Returns the account to the position it started from: cancels an order that never filled, or
    /// sells back the share that did.
    /// </summary>
    /// <remarks>
    /// A method rather than the body of the <c>finally</c> block, so that failing to unwind can be
    /// reported as a failure. Silence here would be the worst outcome of the whole sweep: a position
    /// left open by a tool whose report says everything passed.
    /// </remarks>
    private static async Task CleanUpAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        ConId conId,
        OrderId? placed,
        bool isLong,
        decimal opening,
        CancellationToken cancellationToken)
    {
        if (isLong)
        {
            await probe.RunAsync("POST /iserver/account/{a}/orders (unwind)", async () =>
            {
                var sold = await SubmitAsync(ibkr, account, conId, OrderSide.Sell, cancellationToken);
                var status = await AwaitTerminalStatusAsync(ibkr, sold, cancellationToken);
                if (!IsFilled(status))
                {
                    throw new InvalidOperationException(
                        $"the unwinding sell ended {status.Status}; conid {conId} is one share over {opening}");
                }

                var quantity = await AwaitQuantityAsync(ibkr, account, conId, opening, cancellationToken);
                return quantity == opening
                    ? $"sold {status.CumulativeFill} @ {status.AveragePrice}; position back to {opening}"
                    : throw new InvalidOperationException(
                        $"sold {status.CumulativeFill} @ {status.AveragePrice} but IBKR reports {quantity}, " +
                        $"not the {opening} the account started with");
            });
        }
        else if (placed is { } working)
        {
            // Accepted but never filled: leave nothing working behind.
            await probe.RunAsync("DELETE /iserver/account/{a}/order/{id} (unfilled)", async () =>
                $"{(await ibkr.Orders.CancelAsync(account, working, cancellationToken: cancellationToken)).Message}");
        }
    }

    /// <summary>
    /// Compares the two encodings IBKR sends of one timestamp, and fails when they disagree.
    /// </summary>
    private static string Compare(string textField, Instant? text, string epochField, Instant? epoch)
    {
        if (text is null && epoch is null)
        {
            return $"{textField} and {epochField} both absent";
        }

        if (text is not { } left || epoch is not { } right)
        {
            return $"only {(text is null ? epochField : textField)} was sent";
        }

        var difference = left > right ? left - right : right - left;
        return difference <= EncodingTolerance
            ? $"{textField} and {epochField} agree"
            : throw new InvalidOperationException(
                $"{textField} decoded to {left} but {epochField} to {right}, {difference} apart -- " +
                "one of the two converters reads the wrong format");
    }

    private static async Task<OrderId> SubmitAsync(
        IIbkrTradingClient ibkr,
        AccountId account,
        ConId conId,
        OrderSide side,
        CancellationToken cancellationToken)
    {
        var result = await ibkr.Orders.SubmitAsync(
            account,
            [
                new OrderTicket
                {
                    ConId = conId.Value,
                    OrderType = "MKT",
                    Side = side,
                    TimeInForce = TimeInForce.Day,
                    Quantity = Quantity,
                },
            ],
            OrderReplyPolicy.Manual,
            cancellationToken);

        // A reply message is a question, not a rejection. The loop is bounded because an id answered
        // twice is a bug, and an unbounded confirm loop against a live account is not the place to
        // discover that.
        for (var answered = 0; answered < 8 && result is OrderSubmissionResult.ReplyRequired reply; answered++)
        {
            Console.WriteLine($"      confirming \"{string.Join(" | ", reply.Messages.SelectMany(m => m.Message))}\"");
            result = await ibkr.Orders.ConfirmReplyAsync(reply.Messages[0].Id!, confirmed: true, cancellationToken);
        }

        return result is OrderSubmissionResult.Accepted accepted
            ? accepted.Orders[0].OrderId
            : throw new InvalidOperationException($"IBKR did not accept the order: {OrderChecks.Describe(result)}");
    }

    private static async Task<OrderStatus> AwaitTerminalStatusAsync(
        IIbkrTradingClient ibkr,
        OrderId orderId,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        OrderStatus status;
        do
        {
            await Task.Delay(PollInterval, cancellationToken);
            status = await ibkr.Orders.GetOrderStatusAsync(orderId, cancellationToken);
            if (TerminalStatuses.Contains(status.Status, StringComparer.OrdinalIgnoreCase))
            {
                return status;
            }
        }
        while (elapsed.Elapsed < FillTimeout);

        throw new SkipCheckException($"the order was still {status.Status} after {FillTimeout.TotalSeconds:N0}s");
    }

    /// <summary>
    /// Reads back the execution behind a filled order.
    /// </summary>
    /// <remarks>
    /// Matched on the order identifier where IBKR agrees with itself, and otherwise on the most
    /// recent execution in the instrument. The fallback exists because the identifier IBKR returns
    /// on submission is not documented to be the one it reports the execution under.
    /// </remarks>
    private static async Task<Execution> AwaitExecutionAsync(
        IIbkrTradingClient ibkr,
        OrderId orderId,
        ConId conId,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        IReadOnlyList<Execution> trades;
        do
        {
            // Limited by IBKR to one request every five seconds, so this waits rather than spins.
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            trades = await ibkr.Orders.GetTradesAsync(cancellationToken: cancellationToken);

            if (trades.FirstOrDefault(t => t.OrderId == orderId) is { } matched)
            {
                return matched;
            }

            if (trades.LastOrDefault(t => t.ConId == conId) is { } sameInstrument)
            {
                Console.WriteLine(
                    $"      note: no execution carries order_id {orderId}; " +
                    $"matched on conid instead (order_id {sameInstrument.OrderId})");
                return sameInstrument;
            }
        }
        while (elapsed.Elapsed < FillTimeout);

        throw new SkipCheckException($"no execution appeared within {FillTimeout.TotalSeconds:N0}s of the fill");
    }

    /// <summary>Reads the account's quantity in one instrument, treating no position as zero.</summary>
    private static async Task<decimal> ReadQuantityAsync(
        IIbkrTradingClient ibkr,
        AccountId account,
        ConId conId,
        CancellationToken cancellationToken)
    {
        var positions = await ibkr.Portfolio.GetPositionAsync(account, conId, cancellationToken);
        return positions.Count == 0 ? 0m : positions[0].Quantity ?? 0m;
    }

    /// <summary>
    /// Waits for the position cache to catch up with a fill, and returns what it settled on.
    /// </summary>
    /// <remarks>
    /// Returns the last quantity seen rather than throwing on a timeout, so the caller decides
    /// whether the difference is a problem. IBKR serves positions from a cache that lags the fill.
    /// </remarks>
    private static async Task<decimal> AwaitQuantityAsync(
        IIbkrTradingClient ibkr,
        AccountId account,
        ConId conId,
        decimal expected,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        decimal quantity;
        do
        {
            await Task.Delay(PollInterval, cancellationToken);
            quantity = await ReadQuantityAsync(ibkr, account, conId, cancellationToken);
            if (quantity == expected)
            {
                return quantity;
            }
        }
        while (elapsed.Elapsed < FillTimeout);

        return quantity;
    }

    private static bool IsFilled(OrderStatus status) =>
        string.Equals(status.Status, "Filled", StringComparison.OrdinalIgnoreCase);
}
