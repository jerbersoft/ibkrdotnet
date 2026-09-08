using System.Globalization;
using NodaTime;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// A time span away from the request's start time, as accepted by the <c>period</c> query parameter
/// of <c>GET /iserver/marketdata/history</c>.
/// </summary>
/// <remarks>
/// Supported units are minutes, hours, days, weeks, months and years. Seconds are valid as a bar
/// width but not as a period.
/// </remarks>
public readonly record struct HistoryPeriod : IEquatable<HistoryPeriod>
{
    /// <summary>Creates a period from a count and a unit.</summary>
    /// <param name="count">The number of <paramref name="unit"/>s. Must be positive.</param>
    /// <param name="unit">The unit of the period.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is not positive, or <paramref name="unit"/> is
    /// <see cref="IbkrTimeUnit.Seconds"/>, which IBKR does not accept as a period.
    /// </exception>
    public HistoryPeriod(int count, IbkrTimeUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (unit == IbkrTimeUnit.Seconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unit),
                unit,
                "Seconds is not a valid history period; the smallest supported period unit is Minutes.");
        }

        Count = count;
        Unit = unit;
    }

    /// <summary>The number of <see cref="Unit"/>s spanned by the period.</summary>
    public int Count { get; }

    /// <summary>The unit of the period.</summary>
    public IbkrTimeUnit Unit { get; }

    /// <summary>One day. This is the API default.</summary>
    public static HistoryPeriod OneDay => new(1, IbkrTimeUnit.Days);

    /// <summary>One week.</summary>
    public static HistoryPeriod OneWeek => new(1, IbkrTimeUnit.Weeks);

    /// <summary>One month.</summary>
    public static HistoryPeriod OneMonth => new(1, IbkrTimeUnit.Months);

    /// <summary>One year.</summary>
    public static HistoryPeriod OneYear => new(1, IbkrTimeUnit.Years);

    /// <summary>Creates a period in minutes.</summary>
    /// <param name="count">The number of minutes.</param>
    public static HistoryPeriod Minutes(int count) => new(count, IbkrTimeUnit.Minutes);

    /// <summary>Creates a period in hours.</summary>
    /// <param name="count">The number of hours.</param>
    public static HistoryPeriod Hours(int count) => new(count, IbkrTimeUnit.Hours);

    /// <summary>Creates a period in days.</summary>
    /// <param name="count">The number of days.</param>
    public static HistoryPeriod Days(int count) => new(count, IbkrTimeUnit.Days);

    /// <summary>Creates a period in weeks.</summary>
    /// <param name="count">The number of weeks.</param>
    public static HistoryPeriod Weeks(int count) => new(count, IbkrTimeUnit.Weeks);

    /// <summary>Creates a period in months.</summary>
    /// <param name="count">The number of months.</param>
    public static HistoryPeriod Months(int count) => new(count, IbkrTimeUnit.Months);

    /// <summary>Creates a period in years.</summary>
    /// <param name="count">The number of years.</param>
    public static HistoryPeriod Years(int count) => new(count, IbkrTimeUnit.Years);

    /// <summary>
    /// The period as an exact <see cref="Duration"/>, or <see langword="null"/> when the unit has no
    /// fixed length.
    /// </summary>
    /// <remarks>
    /// Months and years have no fixed length, so they yield <see langword="null"/> rather than a
    /// misleading approximation. Use <see cref="ToNodaPeriod"/> for calendar arithmetic instead.
    /// </remarks>
    public Duration? ToDuration() => Unit switch
    {
        IbkrTimeUnit.Minutes => Duration.FromMinutes((long)Count),
        IbkrTimeUnit.Hours => Duration.FromHours(Count),
        IbkrTimeUnit.Days => Duration.FromDays(Count),
        IbkrTimeUnit.Weeks => Duration.FromDays(Count * 7),
        _ => null,
    };

    /// <summary>The period as a calendar-aware NodaTime <see cref="Period"/>.</summary>
    public Period ToNodaPeriod() => Unit switch
    {
        IbkrTimeUnit.Minutes => Period.FromMinutes(Count),
        IbkrTimeUnit.Hours => Period.FromHours(Count),
        IbkrTimeUnit.Days => Period.FromDays(Count),
        IbkrTimeUnit.Weeks => Period.FromWeeks(Count),
        IbkrTimeUnit.Months => Period.FromMonths(Count),
        IbkrTimeUnit.Years => Period.FromYears(Count),
        _ => throw new InvalidOperationException($"Unit {Unit} has no period representation."),
    };

    /// <summary>Parses a wire value such as <c>1d</c>, <c>6m</c> or <c>2y</c>.</summary>
    /// <param name="value">The wire value.</param>
    /// <exception cref="FormatException"><paramref name="value"/> is not a valid period.</exception>
    public static HistoryPeriod Parse(string value) =>
        TryParse(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a valid IBKR history period.");

    /// <summary>Attempts to parse a wire value such as <c>1d</c>, <c>6m</c> or <c>2y</c>.</summary>
    /// <param name="value">The wire value.</param>
    /// <param name="result">The parsed period when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was parsed.</returns>
    public static bool TryParse(string? value, out HistoryPeriod result)
    {
        result = default;
        if (!IbkrTimeUnitSuffix.TryParse(value, out var count, out var unit) ||
            unit == IbkrTimeUnit.Seconds)
        {
            return false;
        }

        result = new HistoryPeriod(count, unit);
        return true;
    }

    /// <summary>Returns the wire representation, for example <c>6m</c>.</summary>
    public override string ToString() =>
        Count.ToString(CultureInfo.InvariantCulture) + IbkrTimeUnitSuffix.ToSuffix(Unit);
}
