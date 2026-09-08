using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="OrderId"/>, accepting both the JSON number and JSON string
/// encodings that Interactive Brokers uses for order identifiers.
/// </summary>
public sealed class OrderIdConverter : JsonConverter<OrderId>
{
    /// <inheritdoc />
    public override OrderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonReaderNumerics.ReadInt64(ref reader, "an order identifier", typeof(OrderId)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OrderId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }
}
