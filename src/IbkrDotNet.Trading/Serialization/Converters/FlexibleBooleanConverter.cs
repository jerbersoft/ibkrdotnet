using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a boolean that Interactive Brokers may encode as <c>true</c>/<c>false</c>, as the numbers
/// <c>1</c>/<c>0</c>, or as the strings <c>"1"</c>/<c>"0"</c>, <c>"Y"</c>/<c>"N"</c> or
/// <c>"true"</c>/<c>"false"</c>.
/// </summary>
/// <remarks>
/// Fields such as <c>supports_tax_opt</c>, <c>liquidation_trade</c>, <c>is_event_trading</c> and
/// <c>cancelDayOrders</c> all mean "yes or no" but each picks a different encoding. Values are
/// always written as JSON booleans.
/// </remarks>
public sealed class FlexibleBooleanConverter : JsonConverter<bool>
{
    /// <inheritdoc />
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.Number:
                return reader.GetDouble() != 0d;

            case JsonTokenType.String:
                var text = reader.GetString();
                return text switch
                {
                    "1" or "Y" or "y" => true,
                    "0" or "N" or "n" or "" => false,
                    _ when bool.TryParse(text, out var parsed) => parsed,
                    _ => throw IbkrSerializationException.ForValue(
                        text,
                        "true/false, 1/0 or Y/N",
                        typeof(bool)),
                };

            default:
                throw IbkrSerializationException.ForValue(
                    reader.TokenType.ToString(),
                    "true/false, 1/0 or Y/N",
                    typeof(bool));
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBooleanValue(value);
    }
}
