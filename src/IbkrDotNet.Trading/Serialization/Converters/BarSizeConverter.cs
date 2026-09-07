using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="BarSize"/> in its wire form, for example <c>5min</c>.
/// </summary>
public sealed class BarSizeConverter : JsonConverter<BarSize>
{
    /// <inheritdoc />
    public override BarSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "a bar width such as '5min'",
            typeof(BarSize));

        return BarSize.TryParse(text, out var result)
            ? result
            : throw IbkrSerializationException.ForValue(
                text,
                "a bar width such as '5min'",
                typeof(BarSize));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, BarSize value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
