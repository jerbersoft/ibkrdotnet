using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="decimal"/> that Interactive Brokers may encode as a JSON number or as a JSON
/// string.
/// </summary>
/// <remarks>
/// Monetary fields such as <c>price</c>, <c>commission</c> and the account summary amounts arrive as
/// strings on several endpoints and as numbers on others. Prices and balances use
/// <see cref="decimal"/> rather than <see cref="double"/> so that values are not perturbed by binary
/// floating point rounding.
/// </remarks>
public sealed class FlexibleDecimalConverter : JsonConverter<decimal>
{
    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonReaderNumerics.ReadDecimal(ref reader, typeof(decimal));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value);
    }
}
