using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Primitives;

/// <summary>
/// An Interactive Brokers contract identifier ("conid"), uniquely identifying a tradable instrument.
/// </summary>
/// <remarks>
/// IBKR returns conids as JSON numbers on some endpoints and as JSON strings on others (for example
/// <c>/iserver/account/trades</c>), so the JSON converter accepts both and always writes a number.
/// </remarks>
[JsonConverter(typeof(ConIdConverter))]
public readonly record struct ConId(long Value) : IComparable<ConId>, IFormattable
{
    /// <summary>Converts a conid to its underlying numeric value.</summary>
    /// <param name="conId">The conid.</param>
    public static implicit operator long(ConId conId) => conId.Value;

    /// <summary>Converts a numeric value to a conid.</summary>
    /// <param name="value">The numeric conid value.</param>
    public static explicit operator ConId(long value) => new(value);

    /// <summary>Creates a conid from a numeric value.</summary>
    /// <param name="value">The numeric conid value.</param>
    public static ConId FromInt64(long value) => new(value);

    /// <summary>Parses a conid from its textual representation.</summary>
    /// <param name="value">The textual conid.</param>
    /// <exception cref="FormatException"><paramref name="value"/> is not a valid conid.</exception>
    public static ConId Parse(string value) =>
        TryParse(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a valid IBKR contract identifier.");

    /// <summary>Attempts to parse a conid from its textual representation.</summary>
    /// <param name="value">The textual conid.</param>
    /// <param name="result">The parsed conid when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was parsed.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out ConId result)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            result = new ConId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Determines whether one ConId sorts before another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <(ConId left, ConId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one ConId sorts before or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <=(ConId left, ConId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one ConId sorts after another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >(ConId left, ConId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether one ConId sorts after or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >=(ConId left, ConId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(ConId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
}
