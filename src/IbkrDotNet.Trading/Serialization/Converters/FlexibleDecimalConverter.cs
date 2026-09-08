using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a <see cref="decimal"/> that Interactive Brokers may encode as a JSON number, as a JSON
/// string, or as one of its textual stand-ins for an absent value.
/// </summary>
/// <remarks>
/// Monetary fields such as <c>price</c>, <c>commission</c> and the account summary amounts arrive as
/// strings on several endpoints and as numbers on others, and as <c>""</c> wherever the field does
/// not apply -- the limit price of a market order, for instance. Prices and balances use
/// <see cref="decimal"/> rather than <see cref="double"/> so that values are not perturbed by binary
/// floating point rounding.
/// </remarks>
public sealed class FlexibleDecimalConverter : IbkrStructConverterFactory<decimal>
{
    /// <inheritdoc />
    protected override decimal ReadValue(ref Utf8JsonReader reader) =>
        JsonReaderNumerics.ReadDecimal(ref reader, typeof(decimal));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, decimal value) =>
        writer.WriteNumberValue(value);
}
