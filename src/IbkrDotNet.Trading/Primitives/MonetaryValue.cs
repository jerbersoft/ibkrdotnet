using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace IbkrDotNet.Trading.Primitives;

/// <summary>
/// An amount and its currency, parsed from the display-formatted strings the account summary
/// endpoints return — for example <c>"1,288,301 USD"</c>.
/// </summary>
/// <remarks>
/// <c>/iserver/account/{accountId}/summary/balances</c> and its siblings return values formatted for
/// display rather than for computation: thousands separators, a trailing currency code, and
/// non-numeric placeholders such as <c>"n/a"</c>, <c>"Unlimited"</c> and <c>"@ 16:00:00"</c> mixed
/// into the same maps. Parsing is therefore offered as a <c>TryParse</c> rather than done eagerly
/// during deserialization, where a placeholder would have to become either an exception or a silent
/// zero.
/// </remarks>
public readonly record struct MonetaryValue(decimal Amount, string? Currency)
{
    /// <summary>Parses a display-formatted amount such as <c>"1,288,301 USD"</c>.</summary>
    /// <param name="value">The formatted value.</param>
    /// <exception cref="FormatException"><paramref name="value"/> is not a monetary amount.</exception>
    public static MonetaryValue Parse(string value) =>
        TryParse(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a monetary amount.");

    /// <summary>Attempts to parse a display-formatted amount such as <c>"1,288,301 USD"</c>.</summary>
    /// <param name="value">The formatted value.</param>
    /// <param name="result">The parsed value when parsing succeeds.</param>
    /// <returns>
    /// <see langword="false"/> for the non-numeric placeholders IBKR mixes into these maps, such as
    /// <c>"n/a"</c> and <c>"Unlimited"</c>.
    /// </returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out MonetaryValue result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var span = value.AsSpan().Trim();
        var separator = span.LastIndexOf(' ');

        ReadOnlySpan<char> number;
        string? currency;
        if (separator > 0 && IsCurrencyCode(span[(separator + 1)..]))
        {
            number = span[..separator].Trim();
            currency = new string(span[(separator + 1)..]);
        }
        else
        {
            number = span;
            currency = null;
        }

        if (!decimal.TryParse(
                number,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return false;
        }

        result = new MonetaryValue(amount, currency);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() =>
        Currency is null
            ? Amount.ToString(CultureInfo.InvariantCulture)
            : $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency}";

    private static bool IsCurrencyCode(ReadOnlySpan<char> span)
    {
        if (span.Length is < 3 or > 4)
        {
            return false;
        }

        foreach (var c in span)
        {
            if (!char.IsAsciiLetterUpper(c))
            {
                return false;
            }
        }

        return true;
    }
}
