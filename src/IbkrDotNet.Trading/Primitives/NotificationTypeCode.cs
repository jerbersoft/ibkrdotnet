using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Primitives;

/// <summary>
/// The two-character code Interactive Brokers uses to name a category of notification, such as
/// <c>OE</c> for option expiration or <c>DA</c> for the dividends advisory.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not an <see langword="enum"/>. IBKR documents the codes as a closed set of
/// twenty-three, and then contradicts itself in the same reference: the example responses for
/// <c>GET /fyi/settings</c> and <c>GET /fyi/notifications</c> both carry <c>PF</c>, which is not one
/// of the twenty-three. An enum would turn every code IBKR adds, or never wrote down, into a
/// deserialization failure of the whole response.
/// </para>
/// <para>
/// The documented codes are exposed as static members so the common cases are discoverable and spelt
/// correctly, and any other code round-trips unchanged.
/// </para>
/// </remarks>
[JsonConverter(typeof(NotificationTypeCodeConverter))]
public readonly record struct NotificationTypeCode : IComparable<NotificationTypeCode>
{
    /// <summary>Creates a type code.</summary>
    /// <param name="value">The code, for example <c>OE</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty or whitespace.</exception>
    public NotificationTypeCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The code text.</summary>
    public string Value { get; }

    /// <summary>Borrow availability. Code <c>BA</c>.</summary>
    public static NotificationTypeCode BorrowAvailability => new("BA");

    /// <summary>Comparable algo. Code <c>CA</c>.</summary>
    public static NotificationTypeCode ComparableAlgo => new("CA");

    /// <summary>Dividends advisory. Code <c>DA</c>.</summary>
    public static NotificationTypeCode DividendsAdvisory => new("DA");

    /// <summary>Upcoming earnings. Code <c>EA</c>.</summary>
    public static NotificationTypeCode UpcomingEarnings => new("EA");

    /// <summary>Mutual fund advisory. Code <c>MF</c>.</summary>
    public static NotificationTypeCode MutualFundAdvisory => new("MF");

    /// <summary>Option expiration. Code <c>OE</c>.</summary>
    public static NotificationTypeCode OptionExpiration => new("OE");

    /// <summary>Portfolio builder rebalance. Code <c>PR</c>.</summary>
    public static NotificationTypeCode PortfolioBuilderRebalance => new("PR");

    /// <summary>Suspend order on economic event. Code <c>SE</c>.</summary>
    public static NotificationTypeCode SuspendOrderOnEconomicEvent => new("SE");

    /// <summary>Short-term gain turning long term. Code <c>SG</c>.</summary>
    public static NotificationTypeCode ShortTermGainTurningLongTerm => new("SG");

    /// <summary>System messages. Code <c>SM</c>.</summary>
    public static NotificationTypeCode SystemMessages => new("SM");

    /// <summary>Assignment realizing long-term gains. Code <c>T2</c>.</summary>
    public static NotificationTypeCode AssignmentRealizingLongTermGains => new("T2");

    /// <summary>Takeover. Code <c>TO</c>.</summary>
    public static NotificationTypeCode Takeover => new("TO");

    /// <summary>User alert. Code <c>UA</c>.</summary>
    public static NotificationTypeCode UserAlert => new("UA");

    /// <summary>M871 trades. Code <c>M8</c>.</summary>
    public static NotificationTypeCode M871Trades => new("M8");

    /// <summary>Platform use suggestions. Code <c>PS</c>.</summary>
    public static NotificationTypeCode PlatformUseSuggestions => new("PS");

    /// <summary>Unexercised option loss prevention reminder. Code <c>DL</c>.</summary>
    public static NotificationTypeCode UnexercisedOptionLossPrevention => new("DL");

    /// <summary>Position transfer. Code <c>PT</c>.</summary>
    public static NotificationTypeCode PositionTransfer => new("PT");

    /// <summary>Missing cost basis. Code <c>CB</c>.</summary>
    public static NotificationTypeCode MissingCostBasis => new("CB");

    /// <summary>Milestones. Code <c>MS</c>.</summary>
    public static NotificationTypeCode Milestones => new("MS");

    /// <summary>MiFID II 10% depreciation notice. Code <c>TD</c>.</summary>
    public static NotificationTypeCode DepreciationNotice => new("TD");

    /// <summary>Save taxes. Code <c>ST</c>.</summary>
    public static NotificationTypeCode SaveTaxes => new("ST");

    /// <summary>Trade idea. Code <c>TI</c>.</summary>
    public static NotificationTypeCode TradeIdea => new("TI");

    /// <summary>Cash transfer. Code <c>CT</c>.</summary>
    public static NotificationTypeCode CashTransfer => new("CT");

    /// <summary>
    /// Portfolio FYIs. Code <c>PF</c>.
    /// </summary>
    /// <remarks>
    /// Undocumented as a type code, though it appears in IBKR's own example responses for
    /// <c>GET /fyi/settings</c> and <c>GET /fyi/notifications</c>. Included because a caller reading
    /// either of those will meet it immediately.
    /// </remarks>
    public static NotificationTypeCode PortfolioFyis => new("PF");

    /// <summary>Converts a type code to its underlying text.</summary>
    /// <param name="typeCode">The type code.</param>
    public static implicit operator string(NotificationTypeCode typeCode) => typeCode.Value;

    /// <summary>Converts text to a type code.</summary>
    /// <param name="value">The code text.</param>
    public static explicit operator NotificationTypeCode(string value) => new(value);

    /// <summary>Determines whether one code sorts before another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <(NotificationTypeCode left, NotificationTypeCode right) =>
        left.CompareTo(right) < 0;

    /// <summary>Determines whether one code sorts before or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator <=(NotificationTypeCode left, NotificationTypeCode right) =>
        left.CompareTo(right) <= 0;

    /// <summary>Determines whether one code sorts after another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >(NotificationTypeCode left, NotificationTypeCode right) =>
        left.CompareTo(right) > 0;

    /// <summary>Determines whether one code sorts after or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static bool operator >=(NotificationTypeCode left, NotificationTypeCode right) =>
        left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(NotificationTypeCode other) => string.CompareOrdinal(Value, other.Value);

    /// <inheritdoc />
    public bool Equals(NotificationTypeCode other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
