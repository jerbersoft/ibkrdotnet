using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="double"/> that Interactive Brokers may encode as a JSON number or as a JSON
/// string.
/// </summary>
public sealed class FlexibleDoubleConverter : JsonConverter<double>
{
    /// <inheritdoc />
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonReaderNumerics.ReadDouble(ref reader, typeof(double));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value);
    }
}
