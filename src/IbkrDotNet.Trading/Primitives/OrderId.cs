using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Primitives;

/// <summary>
/// An Interactive Brokers order identifier, assigned by IBKR when an order ticket is accepted.
/// </summary>
/// <remarks>
/// As with <see cref="ConId"/>, IBKR returns order identifiers as JSON numbers on some endpoints and
/// as JSON strings on others, so the converter accepts both.
/// </remarks>
[JsonConverter(typeof(OrderIdConverter))]
public readonly record struct OrderId(long Value) : IComparable<OrderId>, IFormattable
{
    /// <summary>Converts an order identifier to its underlying numeric value.</summary>
    /// <param name="orderId">The order identifier.</param>
    public static implicit operator long(OrderId orderId) => orderId.Value;

    /// <summary>Converts a numeric value to an order identifier.</summary>
    /// <param name="value">The numeric order identifier.</param>
    public static explicit operator OrderId(long value) => new(value);

    /// <summary>Parses an order identifier from its textual representation.</summary>
    /// <param name="value">The textual order identifier.</param>
    /// <exception cref="FormatException"><paramref name="value"/> is not a valid order identifier.</exception>
    public static OrderId Parse(string value) =>
        TryParse(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a valid IBKR order identifier.");

    /// <summary>Attempts to parse an order identifier from its textual representation.</summary>
    /// <param name="value">The textual order identifier.</param>
    /// <param name="result">The parsed order identifier when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was parsed.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out OrderId result)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            result = new OrderId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Determines whether one OrderId sorts before another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <(OrderId left, OrderId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one OrderId sorts before or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <=(OrderId left, OrderId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one OrderId sorts after another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >(OrderId left, OrderId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether one OrderId sorts after or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >=(OrderId left, OrderId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(OrderId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
}
