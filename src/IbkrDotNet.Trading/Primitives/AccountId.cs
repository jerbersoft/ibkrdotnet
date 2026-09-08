using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Primitives;

/// <summary>
/// An Interactive Brokers account identifier, such as <c>U1234567</c> or the paper-trading
/// equivalent <c>DU123456</c>.
/// </summary>
[JsonConverter(typeof(AccountIdConverter))]
public readonly record struct AccountId : IComparable<AccountId>
{
    /// <summary>Creates an account identifier.</summary>
    /// <param name="value">The account identifier text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty or whitespace.</exception>
    public AccountId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The account identifier text.</summary>
    public string Value { get; }

    /// <summary>Converts an account identifier to its underlying text.</summary>
    /// <param name="accountId">The account identifier.</param>
    public static implicit operator string(AccountId accountId) => accountId.Value;

    /// <summary>Converts text to an account identifier.</summary>
    /// <param name="value">The account identifier text.</param>
    public static explicit operator AccountId(string value) => new(value);

    /// <summary>Determines whether one AccountId sorts before another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <(AccountId left, AccountId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one AccountId sorts before or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <=(AccountId left, AccountId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one AccountId sorts after another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >(AccountId left, AccountId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether one AccountId sorts after or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >=(AccountId left, AccountId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(AccountId other) =>
        string.CompareOrdinal(Value, other.Value);

    /// <inheritdoc />
    public bool Equals(AccountId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
