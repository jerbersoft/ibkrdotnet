using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads an <see cref="int"/> that Interactive Brokers may encode as a JSON number, as a JSON
/// string, or as one of its textual stand-ins for an absent value.
/// </summary>
public sealed class FlexibleInt32Converter : IbkrStructConverterFactory<int>
{
    /// <inheritdoc />
    protected override int ReadValue(ref Utf8JsonReader reader) =>
        JsonReaderNumerics.ReadInt32(ref reader, typeof(int));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, int value) =>
        writer.WriteNumberValue(value);
}
