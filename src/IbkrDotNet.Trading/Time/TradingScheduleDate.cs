using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// The <c>tradingScheduleDate</c> of a trading schedule entry, which is either a specific calendar
/// date or a marker standing for every occurrence of one weekday.
/// </summary>
/// <remarks>
/// <para>
/// IBKR overloads a date field to express two different things: the dates <c>20000101</c> through
/// <c>20000107</c> stand for "any Saturday" through "any Friday" respectively, and every other value
/// stands for itself. Reading one of the markers as a real date would silently place a recurring
/// weekly schedule in the first week of January 2000.
/// </para>
/// <para>
/// The markers are not arbitrary: 1 January 2000 genuinely was a Saturday, so each marker's weekday
/// is simply its own.
/// </para>
/// </remarks>
[JsonConverter(typeof(TradingScheduleDateConverter))]
public readonly record struct TradingScheduleDate
{
    private static readonly LocalDate MarkerRangeStart = new(2000, 1, 1);
    private static readonly LocalDate MarkerRangeEnd = new(2000, 1, 7);

    private TradingScheduleDate(LocalDate? date, IsoDayOfWeek? recurringDay)
    {
        Date = date;
        RecurringDay = recurringDay;
    }

    /// <summary>The specific date, when the entry is for one.</summary>
    public LocalDate? Date { get; }

    /// <summary>The weekday this entry recurs on, when it is a recurring entry.</summary>
    public IsoDayOfWeek? RecurringDay { get; }

    /// <summary>Whether the entry applies to every occurrence of a weekday.</summary>
    public bool IsRecurring => RecurringDay is not null;

    /// <summary>Creates an entry for a specific date.</summary>
    /// <param name="date">The date.</param>
    public static TradingScheduleDate ForDate(LocalDate date) =>
        IsMarker(date)
            ? new TradingScheduleDate(null, date.DayOfWeek)
            : new TradingScheduleDate(date, null);

    /// <summary>Creates an entry that recurs every week on a given weekday.</summary>
    /// <param name="dayOfWeek">The weekday.</param>
    public static TradingScheduleDate ForWeekday(IsoDayOfWeek dayOfWeek) =>
        new(null, dayOfWeek);

    /// <summary>Whether a date falls in IBKR's marker range for a recurring weekday.</summary>
    /// <param name="date">The date to test.</param>
    public static bool IsMarker(LocalDate date) => date >= MarkerRangeStart && date <= MarkerRangeEnd;

    /// <summary>The wire value: <c>yyyyMMdd</c>, using IBKR's marker date for a recurring entry.</summary>
    public LocalDate ToWireDate() =>
        Date ?? RecurringDay switch
        {
            IsoDayOfWeek.Saturday => MarkerRangeStart,
            IsoDayOfWeek.Sunday => new LocalDate(2000, 1, 2),
            IsoDayOfWeek.Monday => new LocalDate(2000, 1, 3),
            IsoDayOfWeek.Tuesday => new LocalDate(2000, 1, 4),
            IsoDayOfWeek.Wednesday => new LocalDate(2000, 1, 5),
            IsoDayOfWeek.Thursday => new LocalDate(2000, 1, 6),
            IsoDayOfWeek.Friday => MarkerRangeEnd,
            _ => throw new InvalidOperationException("The schedule date has neither a date nor a weekday."),
        };

    /// <inheritdoc />
    public override string ToString() =>
        RecurringDay is { } day ? $"every {day}" : Date?.ToString("uuuu-MM-dd", null) ?? string.Empty;
}
