using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="Duration"/> encoded as a whole number of seconds.
/// </summary>
/// <remarks>
/// Used by <c>barLength</c> and <c>tradingDayDuration</c>.
/// </remarks>
public sealed class DurationSecondsConverter : JsonConverter<Duration>
{
    /// <inheritdoc />
    public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Duration.FromSeconds(
            JsonReaderNumerics.ReadInt64(ref reader, "a number of seconds", typeof(Duration)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue((long)value.TotalSeconds);
    }
}
