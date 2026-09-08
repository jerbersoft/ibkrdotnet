using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="HistoryPeriod"/> in its wire form, for example <c>1d</c>.
/// </summary>
public sealed class HistoryPeriodConverter : IbkrStructConverterFactory<HistoryPeriod>
{
    /// <inheritdoc />
    protected override HistoryPeriod ReadValue(ref Utf8JsonReader reader)
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
    protected override void WriteValue(Utf8JsonWriter writer, HistoryPeriod value)
    {
        writer.WriteStringValue(value.ToString());
    }
}
