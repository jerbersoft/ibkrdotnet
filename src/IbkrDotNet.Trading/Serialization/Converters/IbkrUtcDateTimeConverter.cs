using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="Instant"/> encoded as <c>YYYYMMDD-hh:mm:ss</c> in UTC.
/// </summary>
/// <remarks>
/// Used by <c>trade_time</c>, the historical data <c>startTime</c> (both as a request parameter and
/// in the response) and <c>chartPanStartTime</c>. IBKR documents these values as UTC, so they are
/// read as UTC without applying any zone conversion.
/// </remarks>
public sealed class IbkrUtcDateTimeConverter : IbkrStructConverterFactory<Instant>
{
    /// <inheritdoc />
    protected override Instant ReadValue(ref Utf8JsonReader reader)
    {
        var text = JsonReaderNumerics.ReadRequiredString(ref reader, "YYYYMMDD-hh:mm:ss", typeof(Instant));
        var parsed = IbkrTimePatterns.UtcDateTime.Parse(text);
        if (!parsed.Success)
        {
            throw IbkrSerializationException.ForValue(text, "YYYYMMDD-hh:mm:ss", typeof(Instant));
        }

        return parsed.Value.InUtc().ToInstant();
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Instant value)
    {
        writer.WriteStringValue(IbkrTimePatterns.UtcDateTime.Format(value.InUtc().LocalDateTime));
    }
}
