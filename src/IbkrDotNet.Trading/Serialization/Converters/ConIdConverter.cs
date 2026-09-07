using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="ConId"/>, accepting both the JSON number and JSON string encodings
/// that Interactive Brokers uses for contract identifiers.
/// </summary>
public sealed class ConIdConverter : JsonConverter<ConId>
{
    /// <inheritdoc />
    public override ConId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonReaderNumerics.ReadInt64(ref reader, "a contract identifier", typeof(ConId)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ConId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }

    /// <inheritdoc />
    public override ConId ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        ConId.Parse(reader.GetString() ?? string.Empty);

    /// <inheritdoc />
    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        ConId value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.ToString());
    }
}
