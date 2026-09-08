using System.Globalization;
using System.Text.Json;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads numeric wire values that Interactive Brokers may deliver either as JSON numbers or as JSON
/// strings, sometimes for the same field on different endpoints.
/// </summary>
internal static class JsonReaderNumerics
{
    public static long ReadInt64(ref Utf8JsonReader reader, string expectedFormat, Type targetType)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number when reader.TryGetInt64(out var number):
                return number;

            case JsonTokenType.Number when reader.TryGetDouble(out var asDouble):
                // Some fields arrive with a spurious fractional part (for example "1702317649000.0").
                return checked((long)asDouble);

            case JsonTokenType.String:
                var text = reader.GetString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                throw IbkrSerializationException.ForValue(text, expectedFormat, targetType);

            default:
                throw IbkrSerializationException.ForValue(
                    reader.TokenType.ToString(),
                    expectedFormat,
                    targetType);
        }
    }

    public static int ReadInt32(ref Utf8JsonReader reader, Type targetType)
    {
        var value = ReadInt64(ref reader, "an integer", targetType);
        return value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw IbkrSerializationException.ForValue(
                value.ToString(CultureInfo.InvariantCulture),
                "an integer",
                targetType);
    }

    public static double ReadDouble(ref Utf8JsonReader reader, Type targetType)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();

            case JsonTokenType.String:
                var text = reader.GetString();
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                throw IbkrSerializationException.ForValue(text, "a number", targetType);

            default:
                throw IbkrSerializationException.ForValue(
                    reader.TokenType.ToString(),
                    "a number",
                    targetType);
        }
    }

    public static decimal ReadDecimal(ref Utf8JsonReader reader, Type targetType)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDecimal();

            case JsonTokenType.String:
                var text = reader.GetString();
                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                throw IbkrSerializationException.ForValue(text, "a number", targetType);

            default:
                throw IbkrSerializationException.ForValue(
                    reader.TokenType.ToString(),
                    "a number",
                    targetType);
        }
    }

    public static string ReadRequiredString(ref Utf8JsonReader reader, string expectedFormat, Type targetType)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw IbkrSerializationException.ForValue(
                reader.TokenType.ToString(),
                expectedFormat,
                targetType);
        }

        return reader.GetString()
            ?? throw IbkrSerializationException.ForValue(null, expectedFormat, targetType);
    }
}
