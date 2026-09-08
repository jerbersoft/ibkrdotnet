# Placing orders

Order submission has one shape that surprises people: IBKR can answer a valid order with a **question**, and a
question is not a rejection. Everything below follows from that.

## Four outcomes, not two

```csharp
var result = await ibkr.Orders.SubmitAsync(account, [ticket], cancellationToken: ct);

switch (result)
{
    case OrderSubmissionResult.Accepted accepted:
        return accepted.Orders[0].OrderId;

    case OrderSubmissionResult.ReplyRequired reply:
        // IBKR wants something confirmed before the order goes to work. Not a failure.
        foreach (var message in reply.Messages)
            logger.LogWarning("IBKR asks: {Message}", string.Join(" ", message.Message));
        break;

    case OrderSubmissionResult.Rejected rejected:
        throw new InvalidOperationException(rejected.Reject.Text);

    case OrderSubmissionResult.Failed failed:
        throw new InvalidOperationException(failed.Error);
}
```

`SubmitAsync` returns a discriminated result rather than a bare DTO because the endpoint genuinely returns four
different things: an order acknowledgement, an `error` object, an `advancedOrderReject`, or an array of reply
messages. Flattening those into one model would mean a caller reading `null` fields to work out which happened.

## The reply workflow

A `ReplyRequired` carries messages that are usually precautionary limits configured on your username — order size,
percentage of average daily volume, price relative to the market. Confirm one with its identifier:

```csharp
if (result is OrderSubmissionResult.ReplyRequired reply)
{
    result = await ibkr.Orders.ConfirmReplyAsync(reply.Messages[0].Id!, confirmed: true, ct);
}
```

The default policy is `OrderReplyPolicy.Manual` — the messages come back to you and the decision is yours. That is
deliberate: these prompts carry margin, liquidity and price-constraint warnings, and accepting one is a trading
decision, not plumbing.

```csharp
// Opt in, where the precautionary settings on the username are understood and every warning
// this order can raise has already been decided about.
await ibkr.Orders.SubmitAsync(account, [ticket], OrderReplyPolicy.AutoConfirm, ct);
```

To stop being asked at all, suppress the categories once at the start of the session:

```csharp
await ibkr.Orders.SuppressMessagesAsync(["o163", "o451"], ct);
```

## A ticket

```csharp
var ticket = new OrderTicket
{
    ConId       = conId.Value,
    OrderType   = "LMT",
    Side        = OrderSide.Buy,
    TimeInForce = TimeInForce.Day,
    Quantity    = 100,
    Price       = 165.00m,
};
```

`ConId`, `OrderType`, `Side`, `TimeInForce` and `Quantity` are `required`; the compiler will not let you build a
ticket without them. `Price` is the limit price and `AuxPrice` the stop price, so a stop-limit carries both.

`OrderType` is a string rather than an enum because IBKR's accepted set varies by instrument and venue, and a
closed enum would reject a type that a particular contract genuinely supports. `GetRulesAsync` on the contracts
client reports what an instrument accepts.

## Preview first, sometimes mandatorily

```csharp
var preview = await ibkr.Orders.PreviewAsync(account, [ticket], ct);
```

The preview reports the order's effect on margin and equity without submitting it. Some instruments set
`forceOrderPreview` in their market rules, and for those, submitting without previewing first is rejected outright.

## Modifying

```csharp
await ibkr.Orders.ModifyAsync(account, orderId, replacement, cancellationToken: ct);
```

**The replacement ticket is complete, not a patch.** Every instruction from the original has to be repeated, not
only the fields being changed. Modification is also governed by a different ruleset from submission — inspect it
with `GetRulesAsync` passing `modifyOrder`.

## Brackets and OCA

A bracket is a parent order with children that reference it by `ParentId`, submitted together:

```csharp
await ibkr.Orders.SubmitAsync(account, [parent, takeProfit, stopLoss], cancellationToken: ct);
```

Set `IsSingleGroup = true` on the children to make them one-cancels-all: filling one cancels the others.

## Reading back

```csharp
var open       = await ibkr.Orders.GetOpenOrdersAsync(cancellationToken: ct);
var status     = await ibkr.Orders.GetOrderStatusAsync(orderId, ct);
var executions = await ibkr.Orders.GetTradesAsync(cancellationToken: ct);
```

`GetOpenOrdersAsync` and `GetTradesAsync` are each limited to one request every five seconds, and the client paces
them for you.

`GetOpenOrdersAsync(force: true)` clears IBKR's cache and refreshes from the brokerage backend — and **answers the
forced request with an empty array**. The orders arrive on the request after it. That is IBKR's behaviour, not a
bug here.

## A number that does not apply is an empty string

A market order's `limit_price`, a filled order's `price`: IBKR sends `""` rather than `null` or nothing at all,
and `"None"` and `"N/A"` turn up in the same role elsewhere. Every nullable numeric property absorbs these as
`null`, so one inapplicable field does not fail the whole response. A non-nullable one refuses instead of reading
a plausible, wrong zero.

This was found the only way it could be: by letting a market order fill against a live paper account. See
[Development](development.md) for the `--Fills=true` sweep that does it.
