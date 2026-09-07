using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="HistoryPeriod"/> in its wire form, for example <c>1d</c>.
/// </summary>
public sealed class HistoryPeriodConverter : JsonConverter<HistoryPeriod>
{
    /// <inheritdoc />
    public override HistoryPeriod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "a period such as '1d'",
            typeof(HistoryPeriod));

        return HistoryPeriod.TryParse(text, out var result)
            ? result
            : throw IbkrSerializationException.ForValue(
                text,
                "a period such as '1d'",
                typeof(HistoryPeriod));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HistoryPeriod value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
