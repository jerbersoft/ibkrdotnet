using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class OrdersClientTests
{
    private static readonly AccountId Account = new("DU123456");

    private static OrderTicket Ticket => new()
    {
        ConId = 265598,
        OrderType = "LMT",
        Side = OrderSide.Buy,
        TimeInForce = TimeInForce.Day,
        Quantity = 100,
        Price = 165.00m,
    };

    private const string ReplyMessage = """
        [{"id":"07a13a5a-4a48-44a5-bb25-5ab37b79186c",
          "message":["The following order \"BUY 100 AAPL NASDAQ.NMS @ 165.0\" price exceeds the Percentage constraint of 3%."],
          "isSuppressed":false,
          "messageIds":["o163"]}]
        """;

    [Fact]
    public async Task Serializes_the_ticket_the_way_ibkr_expects()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/submit-new-order.json");
        var client = new OrdersClient(harness.ApiClient);

        await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/DU123456/orders", harness.LastRequest.Path);

        // Side and time in force use IBKR's own tokens, and unset optional fields are omitted.
        Assert.Equal(
            """{"orders":[{"conid":265598,"orderType":"LMT","side":"BUY","tif":"DAY","quantity":100,"price":165.00}]}""",
            harness.LastRequest.Body);
    }

    [Fact]
    public async Task Reads_the_documented_acknowledgement_as_accepted()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/submit-new-order.json");
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        var accepted = Assert.IsType<OrderSubmissionResult.Accepted>(result);
        Assert.Equal(new OrderId(1370093239), accepted.Orders[0].OrderId);
        Assert.Equal("PreSubmitted", accepted.Orders[0].OrderStatus);
    }

    [Fact]
    public async Task Reads_a_reply_message_as_awaiting_confirmation_rather_than_as_a_rejection()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(ReplyMessage);
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        var reply = Assert.IsType<OrderSubmissionResult.ReplyRequired>(result);
        Assert.Equal("07a13a5a-4a48-44a5-bb25-5ab37b79186c", reply.Messages[0].Id);
        Assert.Equal(["o163"], reply.Messages[0].MessageIds);
        Assert.False(reply.Messages[0].IsSuppressed);
    }

    [Fact]
    public async Task Does_not_confirm_a_reply_message_under_the_default_policy()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(ReplyMessage);
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        // These prompts carry margin, liquidity and price-constraint warnings. Accepting one is a
        // trading decision, so exactly one request must have been sent.
        Assert.IsType<OrderSubmissionResult.ReplyRequired>(result);
        Assert.Single(harness.Stub.Requests);
    }

    [Fact]
    public async Task Confirms_a_reply_message_only_when_asked_to()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(ReplyMessage);
        harness.RespondWithFixture("trading-orders/confirm-order-reply.json");
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(
            Account,
            [Ticket],
            OrderReplyPolicy.AutoConfirm,
            TestContext.Current.CancellationToken);

        Assert.IsType<OrderSubmissionResult.Accepted>(result);
        Assert.Equal(2, harness.Stub.Requests.Count);
        Assert.Equal(
            "/v1/api/iserver/reply/07a13a5a-4a48-44a5-bb25-5ab37b79186c",
            harness.Stub.Requests[1].Path);
        Assert.Equal("""{"confirmed":true}""", harness.Stub.Requests[1].Body);
    }

    [Fact]
    public async Task Stops_auto_confirming_before_it_becomes_an_unbounded_loop()
    {
        using var harness = new ClientHarness();
        harness.Stub.AlwaysRespondWith(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ReplyMessage, System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new OrdersClient(harness.ApiClient);

        var ex = await Assert.ThrowsAsync<IbkrApiException>(
            () => client.SubmitAsync(
                Account,
                [Ticket],
                OrderReplyPolicy.AutoConfirm,
                TestContext.Current.CancellationToken));

        Assert.Contains("stopped rather than continuing", ex.Message, StringComparison.Ordinal);
        Assert.Equal(
            OrdersClient.MaxAutomaticReplyConfirmations + 1,
            harness.Stub.Requests.Count);
    }

    [Fact]
    public async Task Distinguishes_an_advanced_rejection_from_an_acknowledgement_by_field_casing()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""
            {"orderId":1370093239,"reqId":"1","text":"Order rejected","type":"WARNING",
             "messageId":"o10151","prompt":true,"options":["Resubmit"],"dismissable":[]}
            """);
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        // An acknowledgement spells it 'order_id'; an advanced rejection spells it 'orderId'.
        var rejected = Assert.IsType<OrderSubmissionResult.Rejected>(result);
        Assert.Equal(new OrderId(1370093239), rejected.Reject.OrderId);
        Assert.Equal(["Resubmit"], rejected.Reject.Options);
    }

    [Fact]
    public async Task Reads_a_bare_error_as_a_failure()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""{"error":"Order size exceeds the limit."}""");
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.SubmitAsync(Account, [Ticket], cancellationToken: TestContext.Current.CancellationToken);

        var failed = Assert.IsType<OrderSubmissionResult.Failed>(result);
        Assert.Equal("Order size exceeds the limit.", failed.Error);
    }

    [Fact]
    public async Task Sends_a_bracket_as_a_single_array_with_the_child_naming_its_parent()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/submit-new-order.json");
        var client = new OrdersClient(harness.ApiClient);

        var parent = Ticket with { ClientOrderId = "parent-1" };
        var child = Ticket with { Side = OrderSide.Sell, ParentId = "parent-1", Price = 175m };

        await client.SubmitAsync(Account, [parent, child], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("\"cOID\":\"parent-1\"", harness.LastRequest.Body!, StringComparison.Ordinal);
        Assert.Contains("\"parentId\":\"parent-1\"", harness.LastRequest.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Modifies_an_order_with_a_single_object_rather_than_an_array()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/modify-open-order.json");
        var client = new OrdersClient(harness.ApiClient);

        await client.ModifyAsync(
            Account,
            new OrderId(987654),
            Ticket with { Price = 170m },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/DU123456/order/987654", harness.LastRequest.Path);
        Assert.StartsWith("""{"conid":265598""", harness.LastRequest.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancels_an_order_with_a_delete()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/cancel-open-order.json");
        var client = new OrdersClient(harness.ApiClient);

        var result = await client.CancelAsync(
            Account,
            new OrderId(1370093239),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/account/DU123456/order/1370093239", harness.LastRequest.Path);
        Assert.Equal("Request was submitted", result.Message);
    }

    [Fact]
    public async Task Reads_the_documented_preview_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/preview-margin-impact.json");
        var client = new OrdersClient(harness.ApiClient);

        var preview = await client.PreviewAsync(Account, [Ticket], TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/DU123456/orders/whatif", harness.LastRequest.Path);
        Assert.Equal("1,977.60 USD (10 Shares)", preview.Amount?.Amount);
        Assert.Equal("1652", preview.InitialMargin?.After);
        Assert.Contains("without having market data", preview.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_documented_open_orders_payload_with_both_execution_time_formats()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-open-orders.json");
        var client = new OrdersClient(harness.ApiClient);

        var orders = await client.GetOpenOrdersAsync(
            ["filled"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/orders", harness.LastRequest.Path);
        Assert.Equal("?filters=filled", harness.LastRequest.Query);

        var order = orders.Orders[0];
        Assert.Equal(new OrderId(1370093238), order.OrderId);
        Assert.Equal(0.8908m, order.AveragePrice);
        Assert.Equal(2499.99m, order.FilledQuantity);

        // The two encodings of the same execution time must agree: 'lastExecutionTime_r' is epoch
        // milliseconds in a string, 'lastExecutionTime' is YYMMDDhhmmss.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1714061006000), order.LastExecutionTime);
        Assert.Equal(order.LastExecutionTime, order.LastExecutionTimeCompact);
    }

    [Fact]
    public async Task Reads_the_documented_order_status_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-order-status.json");
        var client = new OrdersClient(harness.ApiClient);

        var status = await client.GetOrderStatusAsync(new OrderId(1799796559), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/order/status/1799796559", harness.LastRequest.Path);
        Assert.Equal("Filled", status.Status);
        Assert.Equal(192.26m, status.AveragePrice);
        Assert.Equal(Instant.FromUtc(2023, 12, 11, 18, 0, 49), status.OrderTime);
    }

    [Fact]
    public async Task Reads_the_documented_trade_history_payload_with_both_time_formats_agreeing()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-trade-history.json");
        var client = new OrdersClient(harness.ApiClient);

        var trades = await client.GetTradesAsync(7, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/trades", harness.LastRequest.Path);
        Assert.Equal("?days=7", harness.LastRequest.Query);

        var trade = trades[0];
        Assert.Equal("0000e0d5.6576fd38.01.01", trade.ExecutionId);
        Assert.Equal(new ConId(265598), trade.ConId);
        Assert.Equal(192.26m, trade.Price);
        Assert.Equal(1.01m, trade.Commission);
        Assert.False(trade.IsLiquidation);

        // 'trade_time' is YYYYMMDD-hh:mm:ss and 'trade_time_r' is numeric epoch milliseconds.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702317649000), trade.TradeTime);
        Assert.Equal(trade.TradeTime, trade.TradeTimeUtc);
    }

    [Fact]
    public async Task Suppresses_and_restores_order_reply_messages()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/suppress-order-replies.json");
        harness.RespondWithFixture("trading-orders/reset-order-suppression.json");
        var client = new OrdersClient(harness.ApiClient);

        var suppressed = await client.SuppressMessagesAsync(["o163", "o354"], TestContext.Current.CancellationToken);
        var reset = await client.ResetMessageSuppressionAsync(TestContext.Current.CancellationToken);

        Assert.Equal("""{"messageIds":["o163","o354"]}""", harness.Stub.Requests[0].Body);
        Assert.Equal("/v1/api/iserver/questions/suppress", harness.Stub.Requests[0].Path);
        Assert.Equal("/v1/api/iserver/questions/suppress/reset", harness.Stub.Requests[1].Path);
        Assert.Equal("submitted", suppressed.Status);
        Assert.Equal("submitted", reset.Status);
    }

    [Fact]
    public async Task Dismisses_a_server_prompt()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("\"string\"");
        var client = new OrdersClient(harness.ApiClient);

        await client.DismissServerPromptAsync(1370093239, "42", "Yes", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/notification", harness.LastRequest.Path);
        Assert.Equal("""{"orderId":1370093239,"reqId":"42","text":"Yes"}""", harness.LastRequest.Body);
    }

    // ---- Captured from a live gateway -----------------------------------------------------------
    //
    // The fixtures below end '.live.json' because IBKR's published examples do not contain what they
    // contain. Each one records a market order filling against a paper account, which is the only way
    // to reach the execution timestamps and the empty-string prices; the documented examples show
    // neither.

    [Fact]
    public async Task Reads_a_live_execution_whose_two_time_encodings_agree()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-trade-history.live.json");
        var client = new OrdersClient(harness.ApiClient);

        var trades = await client.GetTradesAsync(cancellationToken: TestContext.Current.CancellationToken);

        var trade = trades[0];
        Assert.Equal("0000dc8f.6b9035f5.01.01", trade.ExecutionId);
        Assert.Equal(new OrderId(245323648), trade.OrderId);
        Assert.Equal("B", trade.Side);
        Assert.Equal(317.56m, trade.Price);

        // The claim this fixture exists to pin. IBKR sends the same moment twice, as
        // 'trade_time' in YYYYMMDD-hh:mm:ss and 'trade_time_r' in epoch milliseconds, and a
        // converter reading the text as anything but UTC would put the two hours apart. Against
        // the live gateway they landed on the same instant to the second.
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 13, 58, 11), trade.TradeTimeUtc);
        Assert.Equal(trade.TradeTimeUtc, trade.TradeTime);

        // Undocumented: the resulting position, which IBKR's example does not carry.
        Assert.Equal(11m, trade.ResultingPosition);
    }

    [Fact]
    public async Task Reads_a_live_market_order_whose_limit_price_is_an_empty_string()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-order-status.live.json");
        var client = new OrdersClient(harness.ApiClient);

        var status = await client.GetOrderStatusAsync(
            new OrderId(245323648), TestContext.Current.CancellationToken);

        // A market order has no limit price, and IBKR says so with "" rather than null or an
        // absent member. Before this was handled the whole response failed to deserialize.
        Assert.Null(status.LimitPrice);

        Assert.Equal("Filled", status.Status);
        Assert.Equal(317.56m, status.AveragePrice);
        Assert.Equal(1m, status.CumulativeFill);

        // 'order_time' is YYMMDDhhmmss, the one execution timestamp with no epoch twin to check it
        // against. The fill it belongs to is dated one second later, which is what places it in UTC.
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 13, 58, 10), status.OrderTime);
    }

    [Fact]
    public async Task Reads_a_live_filled_order_whose_price_is_an_empty_string()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-orders/get-open-orders.live.json");
        var client = new OrdersClient(harness.ApiClient);

        var orders = await client.GetOpenOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        var order = orders.Orders[0];
        Assert.Null(order.Price);
        Assert.Equal("Filled", order.Status);
        Assert.Equal(317.56m, order.AveragePrice);
        Assert.Equal(1m, order.FilledQuantity);
        Assert.Equal(0m, order.RemainingQuantity);

        // As with the execution: 'lastExecutionTime' is YYMMDDhhmmss, 'lastExecutionTime_r' is
        // epoch milliseconds, and the two have to name the same moment.
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 13, 58, 10), order.LastExecutionTimeCompact);
        Assert.Equal(order.LastExecutionTimeCompact, order.LastExecutionTime);
    }

    [Fact]
    public async Task Refuses_a_submission_with_no_tickets()
    {
        using var harness = new ClientHarness();
        var client = new OrdersClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SubmitAsync(Account, [], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Stub.Requests);
    }
}
