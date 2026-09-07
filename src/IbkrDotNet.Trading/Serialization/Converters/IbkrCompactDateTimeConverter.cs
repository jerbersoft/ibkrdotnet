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
public sealed class IbkrCompactDateTimeConverter : JsonConverter<Instant>
{
    /// <inheritdoc />
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(IbkrTimePatterns.CompactDateTime.Format(value.InUtc().LocalDateTime));
    }
}
