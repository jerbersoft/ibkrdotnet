using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="double"/> that Interactive Brokers may encode as a JSON number, as a JSON
/// string, or as one of its textual stand-ins for an absent value.
/// </summary>
public sealed class FlexibleDoubleConverter : IbkrStructConverterFactory<double>
{
    /// <inheritdoc />
    protected override double ReadValue(ref Utf8JsonReader reader) =>
        JsonReaderNumerics.ReadDouble(ref reader, typeof(double));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, double value) =>
        writer.WriteNumberValue(value);
}
