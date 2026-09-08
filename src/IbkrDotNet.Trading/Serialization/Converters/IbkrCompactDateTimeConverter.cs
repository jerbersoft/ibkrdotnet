using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="Instant"/> encoded as <c>YYMMDDhhmmss</c> in UTC.
/// </summary>
/// <remarks>
/// Used by <c>lastExecutionTime</c> and the alert <c>order_time</c>. The two-digit year is resolved
/// into the 2000s. Where an object also carries the <c>_r</c> epoch-millisecond variant of the same
/// value, prefer that field: it is unambiguous and has no century assumption.
/// </remarks>
public sealed class IbkrCompactDateTimeConverter : IbkrStructConverterFactory<Instant>
{
    /// <inheritdoc />
    protected override Instant ReadValue(ref Utf8JsonReader reader)
    {
        var text = JsonReaderNumerics.ReadRequiredString(ref reader, "YYMMDDhhmmss", typeof(Instant));
        var parsed = IbkrTimePatterns.CompactDateTime.Parse(text);
        if (!parsed.Success)
        {
            throw IbkrSerializationException.ForValue(text, "YYMMDDhhmmss", typeof(Instant));
        }

        return parsed.Value.InUtc().ToInstant();
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Instant value)
    {
        writer.WriteStringValue(IbkrTimePatterns.CompactDateTime.Format(value.InUtc().LocalDateTime));
    }
}
