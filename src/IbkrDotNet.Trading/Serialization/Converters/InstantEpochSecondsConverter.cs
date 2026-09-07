using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="Instant"/> encoded as seconds since the Unix epoch.
/// </summary>
/// <remarks>
/// Used by fields such as the account ledger's <c>timestamp</c>, <c>last_trade_time</c>,
/// <c>payout_time</c> and <c>release_time</c>. Values arriving as JSON strings are accepted;
/// values are always written as JSON numbers.
/// </remarks>
public sealed class InstantEpochSecondsConverter : JsonConverter<Instant>
{
    /// <inheritdoc />
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Instant.FromUnixTimeSeconds(
            JsonReaderNumerics.ReadInt64(ref reader, "seconds since the Unix epoch", typeof(Instant)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
    }
}
