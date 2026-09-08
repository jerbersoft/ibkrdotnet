using NodaTime;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// When an SSO session expires, however Interactive Brokers chose to say it.
/// </summary>
/// <remarks>
/// <para>
/// IBKR documents <c>EXPIRES</c> on <c>GET /sso/validate</c> as "the time until SSO session
/// expiration in milliseconds", and its own example shows <c>415890</c> — just under seven minutes.
/// A live Client Portal Gateway instead sends an absolute epoch-millisecond timestamp for the same
/// field.
/// </para>
/// <para>
/// Both are numbers of milliseconds, so neither shape fails to deserialize: read as a duration, an
/// absolute timestamp becomes a session that lasts fifty-six years, and nothing complains. Rather
/// than pick one and be silently wrong against the other host, this keeps whichever IBKR sent and
/// makes the caller's clock the thing that reconciles them.
/// </para>
/// </remarks>
public readonly record struct SsoExpiry
{
    /// <summary>
    /// Above this, a millisecond count is read as an epoch timestamp rather than a duration.
    /// </summary>
    /// <remarks>
    /// 10^12 milliseconds is September 2001 as a timestamp and just under thirty-two years as a
    /// duration. No SSO session lasts thirty-two years and no IBKR timestamp predates 2001, so
    /// magnitude separates the two shapes with a wide margin either side.
    /// </remarks>
    private const long AbsoluteThresholdMilliseconds = 1_000_000_000_000L;

    private readonly long _milliseconds;
    private readonly bool _isAbsolute;

    private SsoExpiry(long milliseconds, bool isAbsolute)
    {
        _milliseconds = milliseconds;
        _isAbsolute = isAbsolute;
    }

    /// <summary>How long the session has left, when IBKR sent a duration. Null when it sent an instant.</summary>
    public Duration? Remaining => _isAbsolute ? null : Duration.FromMilliseconds(_milliseconds);

    /// <summary>When the session expires, when IBKR sent an instant. Null when it sent a duration.</summary>
    public Instant? At => _isAbsolute ? Instant.FromUnixTimeMilliseconds(_milliseconds) : null;

    /// <summary>The value exactly as IBKR sent it.</summary>
    public long TotalMilliseconds => _milliseconds;

    /// <summary>Reads a raw millisecond value, deciding by magnitude which shape it is.</summary>
    /// <param name="milliseconds">The value from the wire.</param>
    public static SsoExpiry FromMilliseconds(long milliseconds) =>
        new(milliseconds, milliseconds >= AbsoluteThresholdMilliseconds);

    /// <summary>An expiry expressed as time remaining.</summary>
    /// <param name="remaining">How long the session has left.</param>
    public static SsoExpiry FromRemaining(Duration remaining) =>
        new((long)remaining.TotalMilliseconds, isAbsolute: false);

    /// <summary>An expiry expressed as a moment.</summary>
    /// <param name="instant">When the session expires.</param>
    public static SsoExpiry FromInstant(Instant instant) =>
        new(instant.ToUnixTimeMilliseconds(), isAbsolute: true);

    /// <summary>When the session expires, converting a duration against the given moment if needed.</summary>
    /// <param name="now">The current moment, from <see cref="IClock"/>.</param>
    public Instant ExpiresAt(Instant now) => At ?? now + Remaining!.Value;

    /// <summary>How long the session has left, measured from the given moment if needed.</summary>
    /// <param name="now">The current moment, from <see cref="IClock"/>.</param>
    public Duration ExpiresIn(Instant now) => Remaining ?? At!.Value - now;

    /// <inheritdoc />
    public override string ToString() =>
        _isAbsolute ? $"at {At}" : $"in {Remaining}";
}
