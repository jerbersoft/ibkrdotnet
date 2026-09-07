using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="LocalTime"/> encoded as <c>HHmm</c>.
/// </summary>
/// <remarks>
/// Used by the trading schedule's <c>openingTime</c>, <c>closingTime</c> and
/// <c>clearingCycleEndTime</c>. These are wall-clock times in the exchange's own time zone, which
/// the enclosing schedule reports separately as an IANA zone id — combine them with
/// <see cref="DateTimeZone.AtStartOfDay"/> or <c>LocalDate.At(time).InZoneLeniently(zone)</c> to get
/// an absolute instant.
/// </remarks>
public sealed class IbkrLocalTimeConverter : JsonConverter<LocalTime>
{
    /// <inheritdoc />
    public override LocalTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = JsonReaderNumerics.ReadRequiredString(ref reader, "HHmm", typeof(LocalTime));
        var parsed = IbkrTimePatterns.HourMinute.Parse(text);
        if (!parsed.Success)
        {
            throw IbkrSerializationException.ForValue(text, "HHmm", typeof(LocalTime));
        }

        return parsed.Value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(IbkrTimePatterns.HourMinute.Format(value));
    }
}
