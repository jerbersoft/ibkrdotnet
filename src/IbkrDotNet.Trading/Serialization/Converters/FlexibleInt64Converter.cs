using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="long"/> that Interactive Brokers may encode as a JSON number or as a JSON
/// string.
/// </summary>
public sealed class FlexibleInt64Converter : JsonConverter<long>
{
    /// <inheritdoc />
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonReaderNumerics.ReadInt64(ref reader, "an integer", typeof(long));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value);
    }
}
