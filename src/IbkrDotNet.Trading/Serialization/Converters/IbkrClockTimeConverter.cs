using System.Text.Json;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="LocalTime"/> encoded on a twelve-hour clock, such as
/// <c>12:00 AM</c> or <c>4:15 PM</c>.
/// </summary>
/// <remarks>
/// <para>
/// Used only by the event contract trading schedule's <c>open</c> and <c>close</c>. Every other
/// wall-clock time on the API is <c>HHmm</c> and belongs to <see cref="IbkrLocalTimeConverter" />.
/// </para>
/// <para>
/// These are wall-clock times in the exchange's own zone, which the enclosing schedule reports
/// separately -- see <c>EventContractTradingSchedule.TimeZone</c>. A close of <c>11:59 PM</c> is the
/// last minute of the day rather than midnight, so a session that runs to the end of the day is one
/// minute short when measured naively.
/// </para>
/// </remarks>
public sealed class IbkrClockTimeConverter : IbkrStructConverterFactory<LocalTime>
{
    /// <inheritdoc />
    protected override LocalTime ReadValue(ref Utf8JsonReader reader)
    {
        var text = JsonReaderNumerics.ReadRequiredString(ref reader, "h:mm tt", typeof(LocalTime));
        var parsed = IbkrTimePatterns.ClockTime.Parse(text);
        if (!parsed.Success)
        {
            throw IbkrSerializationException.ForValue(text, "h:mm tt", typeof(LocalTime));
        }

        return parsed.Value;
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, LocalTime value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(IbkrTimePatterns.ClockTime.Format(value));
    }
}
