using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="BarSize"/> in its wire form, for example <c>5min</c>.
/// </summary>
public sealed class BarSizeConverter : IbkrStructConverterFactory<BarSize>
{
    /// <inheritdoc />
    protected override BarSize ReadValue(ref Utf8JsonReader reader)
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
    protected override void WriteValue(Utf8JsonWriter writer, BarSize value)
    {
        writer.WriteStringValue(value.ToString());
    }
}
