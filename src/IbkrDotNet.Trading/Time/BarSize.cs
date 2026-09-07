using System.Globalization;
using NodaTime;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// The unit of a <see cref="BarSize"/> or <see cref="HistoryPeriod"/>.
/// </summary>
public enum IbkrTimeUnit
{
    /// <summary>Seconds. Wire suffix <c>S</c>. Valid for bar widths only.</summary>
    Seconds,

    /// <summary>Minutes. Wire suffix <c>min</c>.</summary>
    Minutes,

    /// <summary>Hours. Wire suffix <c>h</c>.</summary>
    Hours,

    /// <summary>Days. Wire suffix <c>d</c>.</summary>
    Days,

    /// <summary>Weeks. Wire suffix <c>w</c>.</summary>
    Weeks,

    /// <summary>Months. Wire suffix <c>m</c>.</summary>
    Months,

    /// <summary>Years. Wire suffix <c>y</c>. Valid for periods only.</summary>
    Years,
}

/// <summary>
/// The width of a historical bar, as accepted by the <c>bar</c> query parameter of
/// <c>GET /iserver/marketdata/history</c>.
/// </summary>
/// <remarks>
/// Supported units are seconds, minutes, hours, days, weeks and months. IBKR does not require the
/// bar width to divide the requested period evenly; partial bars may be returned.
/// </remarks>
public readonly record struct BarSize : IEquatable<BarSize>
{
    /// <summary>Creates a bar width from a count and a unit.</summary>
    /// <param name="count">The number of <paramref name="unit"/>s. Must be positive.</param>
    /// <param name="unit">The unit of the bar width.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is not positive, or <paramref name="unit"/> is
    /// <see cref="IbkrTimeUnit.Years"/>, which IBKR does not accept as a bar width.
    /// </exception>
    public BarSize(int count, IbkrTimeUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (unit == IbkrTimeUnit.Years)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unit),
                unit,
                "Years is not a valid bar width; the largest supported bar unit is Months.");
        }

        Count = count;
        Unit = unit;
    }

    /// <summary>The number of <see cref="Unit"/>s spanned by each bar.</summary>
    public int Count { get; }

    /// <summary>The unit of the bar width.</summary>
    public IbkrTimeUnit Unit { get; }

    /// <summary>A one-second bar.</summary>
    public static BarSize OneSecond => new(1, IbkrTimeUnit.Seconds);

    /// <summary>A one-minute bar. This is the API default.</summary>
    public static BarSize OneMinute => new(1, IbkrTimeUnit.Minutes);

    /// <summary>A five-minute bar.</summary>
    public static BarSize FiveMinutes => new(5, IbkrTimeUnit.Minutes);

    /// <summary>A one-hour bar.</summary>
    public static BarSize OneHour => new(1, IbkrTimeUnit.Hours);

    /// <summary>A one-day bar.</summary>
    public static BarSize OneDay => new(1, IbkrTimeUnit.Days);

    /// <summary>A one-week bar.</summary>
    public static BarSize OneWeek => new(1, IbkrTimeUnit.Weeks);

    /// <summary>A one-month bar.</summary>
    public static BarSize OneMonth => new(1, IbkrTimeUnit.Months);

    /// <summary>Creates a bar width in seconds.</summary>
    /// <param name="count">The number of seconds.</param>
    public static BarSize Seconds(int count) => new(count, IbkrTimeUnit.Seconds);

    /// <summary>Creates a bar width in minutes.</summary>
    /// <param name="count">The number of minutes.</param>
    public static BarSize Minutes(int count) => new(count, IbkrTimeUnit.Minutes);

    /// <summary>Creates a bar width in hours.</summary>
    /// <param name="count">The number of hours.</param>
    public static BarSize Hours(int count) => new(count, IbkrTimeUnit.Hours);

    /// <summary>Creates a bar width in days.</summary>
    /// <param name="count">The number of days.</param>
    public static BarSize Days(int count) => new(count, IbkrTimeUnit.Days);

    /// <summary>Creates a bar width in weeks.</summary>
    /// <param name="count">The number of weeks.</param>
    public static BarSize Weeks(int count) => new(count, IbkrTimeUnit.Weeks);

    /// <summary>Creates a bar width in months.</summary>
    /// <param name="count">The number of months.</param>
    public static BarSize Months(int count) => new(count, IbkrTimeUnit.Months);

    /// <summary>
    /// The bar width as an exact <see cref="Duration"/>, or <see langword="null"/> when the unit has
    /// no fixed length.
    /// </summary>
    /// <remarks>
    /// Months have no fixed length, so <see cref="IbkrTimeUnit.Months"/> yields
    /// <see langword="null"/> rather than a misleading approximation.
    /// </remarks>
    public Duration? ToDuration() => Unit switch
    {
        IbkrTimeUnit.Seconds => Duration.FromSeconds((long)Count),
        IbkrTimeUnit.Minutes => Duration.FromMinutes((long)Count),
        IbkrTimeUnit.Hours => Duration.FromHours(Count),
        IbkrTimeUnit.Days => Duration.FromDays(Count),
        IbkrTimeUnit.Weeks => Duration.FromDays(Count * 7),
        _ => null,
    };

    /// <summary>Parses a wire value such as <c>5min</c>, <c>1h</c> or <c>30S</c>.</summary>
    /// <param name="value">The wire value.</param>
    /// <exception cref="FormatException"><paramref name="value"/> is not a valid bar width.</exception>
    public static BarSize Parse(string value) =>
        TryParse(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a valid IBKR bar width.");

    /// <summary>Attempts to parse a wire value such as <c>5min</c>, <c>1h</c> or <c>30S</c>.</summary>
    /// <param name="value">The wire value.</param>
    /// <param name="result">The parsed bar width when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was parsed.</returns>
    public static bool TryParse(string? value, out BarSize result)
    {
        result = default;
        if (!IbkrTimeUnitSuffix.TryParse(value, out var count, out var unit) ||
            unit == IbkrTimeUnit.Years)
        {
            return false;
        }

        result = new BarSize(count, unit);
        return true;
    }

    /// <summary>Returns the wire representation, for example <c>5min</c>.</summary>
    public override string ToString() =>
        Count.ToString(CultureInfo.InvariantCulture) + IbkrTimeUnitSuffix.ToSuffix(Unit);
}
