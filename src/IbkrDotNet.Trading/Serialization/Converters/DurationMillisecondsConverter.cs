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
public sealed class DurationMillisecondsConverter : JsonConverter<Duration>
{
    /// <inheritdoc />
    public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Duration.FromMilliseconds(
            JsonReaderNumerics.ReadInt64(ref reader, "a number of milliseconds", typeof(Duration)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue((long)value.TotalMilliseconds);
    }
}
