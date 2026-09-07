using System.Text.Json;
using IbkrDotNet.Trading.Serialization;

namespace IbkrDotNet.Trading.Models.Orders;

/// <summary>
/// Decides which of the four shapes IBKR returned from an order submission.
/// </summary>
/// <remarks>
/// The discriminators are the field names themselves. An acknowledgement uses snake case
/// (<c>order_id</c>) while an advanced rejection uses camel case (<c>orderId</c>) for the same
/// concept, which is the only thing separating them.
/// </remarks>
internal static class OrderSubmissionResultParser
{
    public static OrderSubmissionResult Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => ParseArray(root, body),
            JsonValueKind.Object => ParseObject(root, body),
            _ => throw new IbkrSerializationException(
                $"An order submission returned a JSON {root.ValueKind}, which is none of the shapes " +
                "IBKR documents for this endpoint.")
            {
                RawValue = body,
            },
        };
    }

    private static OrderSubmissionResult ParseArray(JsonElement root, string body)
    {
        if (root.GetArrayLength() == 0)
        {
            return new OrderSubmissionResult.Accepted([]);
        }

        var first = root[0];

        if (HasProperty(first, "id") && HasProperty(first, "message"))
        {
            return new OrderSubmissionResult.ReplyRequired(
                Deserialize<List<OrderReplyMessage>>(root, body));
        }

        if (HasProperty(first, "error") && !HasProperty(first, "order_id"))
        {
            return new OrderSubmissionResult.Failed(
                first.GetProperty("error").GetString() ?? "Order submission was not successful.");
        }

        return new OrderSubmissionResult.Accepted(Deserialize<List<OrderConfirmation>>(root, body));
    }

    private static OrderSubmissionResult ParseObject(JsonElement root, string body)
    {
        if (HasProperty(root, "error") &&
            root.GetProperty("error").ValueKind == JsonValueKind.String &&
            !HasProperty(root, "order_id"))
        {
            return new OrderSubmissionResult.Failed(
                root.GetProperty("error").GetString() ?? "Order submission was not successful.");
        }

        // 'orderId' in camel case marks an advanced rejection; an acknowledgement spells the same
        // concept 'order_id'.
        if (HasProperty(root, "orderId") || HasProperty(root, "messageId") || HasProperty(root, "prompt"))
        {
            return new OrderSubmissionResult.Rejected(Deserialize<AdvancedOrderReject>(root, body));
        }

        if (HasProperty(root, "id") && HasProperty(root, "message"))
        {
            return new OrderSubmissionResult.ReplyRequired([Deserialize<OrderReplyMessage>(root, body)]);
        }

        return new OrderSubmissionResult.Accepted([Deserialize<OrderConfirmation>(root, body)]);
    }

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private static T Deserialize<T>(JsonElement element, string body)
    {
        try
        {
            return element.Deserialize<T>(IbkrJson.Options)
                ?? throw new IbkrSerializationException(
                    $"An order submission response could not be read as {typeof(T).Name}.")
                {
                    RawValue = body,
                };
        }
        catch (JsonException ex)
        {
            throw new IbkrSerializationException(
                $"An order submission response could not be read as {typeof(T).Name}: {ex.Message}",
                ex)
            {
                RawValue = body,
            };
        }
    }
}
