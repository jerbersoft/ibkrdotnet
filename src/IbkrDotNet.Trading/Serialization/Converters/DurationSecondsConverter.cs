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
public sealed class DurationSecondsConverter : IbkrStructConverterFactory<Duration>
{
    /// <inheritdoc />
    protected override Duration ReadValue(ref Utf8JsonReader reader) =>
        Duration.FromSeconds(
            JsonReaderNumerics.ReadInt64(ref reader, "a number of seconds", typeof(Duration)));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Duration value)
    {
        writer.WriteNumberValue((long)value.TotalSeconds);
    }
}
