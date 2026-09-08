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
public sealed class InstantEpochMillisecondsConverter : IbkrStructConverterFactory<Instant>
{
    /// <inheritdoc />
    protected override Instant ReadValue(ref Utf8JsonReader reader) =>
        Instant.FromUnixTimeMilliseconds(
            JsonReaderNumerics.ReadInt64(ref reader, "milliseconds since the Unix epoch", typeof(Instant)));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Instant value)
    {
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
    }
}
