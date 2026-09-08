using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="long"/> that Interactive Brokers may encode as a JSON number, as a JSON
/// string, or as one of its textual stand-ins for an absent value.
/// </summary>
public sealed class FlexibleInt64Converter : IbkrStructConverterFactory<long>
{
    /// <inheritdoc />
    protected override long ReadValue(ref Utf8JsonReader reader) =>
        JsonReaderNumerics.ReadInt64(ref reader, "an integer", typeof(long));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, long value) =>
        writer.WriteNumberValue(value);
}
