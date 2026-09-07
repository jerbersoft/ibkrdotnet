using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="Duration"/> encoded as a whole number of milliseconds.
/// </summary>
/// <remarks>
/// Used by <c>mktDataDelay</c>, <c>travelTime</c>, the tickle response's <c>ssoExpires</c> and the
/// SSO validation response's <c>EXPIRES</c>.
/// </remarks>
public sealed class DurationMillisecondsConverter : IbkrStructConverterFactory<Duration>
{
    /// <inheritdoc />
    protected override Duration ReadValue(ref Utf8JsonReader reader) =>
        Duration.FromMilliseconds(
            JsonReaderNumerics.ReadInt64(ref reader, "a number of milliseconds", typeof(Duration)));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Duration value)
    {
        writer.WriteNumberValue((long)value.TotalMilliseconds);
    }
}
