using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Streaming;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads one payload of an <c>ntf</c> message as a
/// <see cref="StreamingNotificationArgs.Notice"/> or a
/// <see cref="StreamingNotificationArgs.Prompt"/>.
/// </summary>
/// <remarks>
/// IBKR marks neither shape with a type tag, so the payload is told by the fields it carries. The
/// test is the one <c>POST /iserver/account/{accountId}/orders</c> already needs to tell an advanced
/// rejection from an acknowledgement, and for the same reason: a question about an order names the
/// order, the question, or both.
/// </remarks>
public sealed class StreamingNotificationArgsConverter : JsonConverter<StreamingNotificationArgs>
{
    /// <inheritdoc />
    public override StreamingNotificationArgs? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw IbkrSerializationException.ForValue(
                reader.TokenType.ToString(),
                "a notification object",
                typeof(StreamingNotificationArgs));
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        // A notice carries none of these three, so anything that carries one is read as a prompt:
        // dropping a question on the floor is worse than reading a notice that gained a field.
        return Has(root, "prompt") || Has(root, "orderId") || Has(root, "messageId")
            ? root.Deserialize<StreamingNotificationArgs.Prompt>(options)
            : root.Deserialize<StreamingNotificationArgs.Notice>(options);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        StreamingNotificationArgs value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Written as whichever shape it is: the concrete types carry no converter of their own, so
        // this does not come back round.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private static bool Has(JsonElement element, string name) => element.TryGetProperty(name, out _);
}
