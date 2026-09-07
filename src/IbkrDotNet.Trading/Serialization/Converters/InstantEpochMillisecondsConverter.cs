using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="Instant"/> encoded as milliseconds since the Unix epoch.
/// </summary>
/// <remarks>
/// Used by fields such as <c>trade_time_r</c> (a JSON number), <c>lastExecutionTime_r</c> (a JSON
/// string containing a number), historical bar timestamps (<c>t</c>) and a watchlist's
/// <c>modified</c>. Both encodings are accepted; values are always written as JSON numbers.
/// </remarks>
public sealed class InstantEpochMillisecondsConverter : JsonConverter<Instant>
{
    /// <inheritdoc />
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Instant.FromUnixTimeMilliseconds(
            JsonReaderNumerics.ReadInt64(ref reader, "milliseconds since the Unix epoch", typeof(Instant)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
    }
}
