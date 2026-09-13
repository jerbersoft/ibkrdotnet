using System.Globalization;
using System.Text.Json;

namespace IbkrDotNet.Trading.Models.MarketData;

/// <summary>
/// Reads the tick-identifier-keyed data points that <see cref="MarketDataSnapshot"/> and
/// <see cref="MarketDataUpdate"/> both carry, so the two read them identically.
/// </summary>
internal static class MarketDataValues
{
    /// <summary>Returns a data point as text, exactly as IBKR sent it.</summary>
    public static string? GetString(IDictionary<string, JsonElement> fields, string field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (!fields.TryGetValue(field, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
    }

    /// <summary>Returns a data point as a number, discarding any leading marker letter.</summary>
    public static decimal? GetDecimal(IDictionary<string, JsonElement> fields, string field)
    {
        var text = GetString(fields, field);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // A price may be prefixed to mark its nature, for example "C212.11" for a previous close.
        var span = text.AsSpan().Trim();
        while (span.Length > 0 && char.IsAsciiLetter(span[0]))
        {
            span = span[1..];
        }

        return decimal.TryParse(span, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
