using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Time;

public class HistoryPeriodTests
{
    [Theory]
    [InlineData("1min")]
    [InlineData("6h")]
    [InlineData("1d")]
    [InlineData("2w")]
    [InlineData("6m")]
    [InlineData("2y")]
    public void Round_trips_through_parsing(string wire)
    {
        Assert.Equal(wire, HistoryPeriod.Parse(wire).ToString());
    }

    [Fact]
    public void Rejects_seconds_which_are_valid_only_as_a_bar_width()
    {
        Assert.False(HistoryPeriod.TryParse("30S", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoryPeriod(30, IbkrTimeUnit.Seconds));
    }

    [Fact]
    public void Converts_to_an_exact_duration_where_one_exists()
    {
        Assert.Equal(Duration.FromDays(7), HistoryPeriod.OneWeek.ToDuration());
    }

    [Fact]
    public void Has_no_duration_for_calendar_units()
    {
        Assert.Null(HistoryPeriod.OneMonth.ToDuration());
        Assert.Null(HistoryPeriod.OneYear.ToDuration());
    }

    [Fact]
    public void Exposes_calendar_units_as_a_noda_period_instead()
    {
        Assert.Equal(Period.FromMonths(6), HistoryPeriod.Months(6).ToNodaPeriod());
        Assert.Equal(Period.FromYears(2), HistoryPeriod.Years(2).ToNodaPeriod());
    }

    [Fact]
    public void A_noda_period_makes_calendar_arithmetic_correct_across_a_month_boundary()
    {
        var start = new LocalDate(2024, 1, 31);

        Assert.Equal(new LocalDate(2024, 2, 29), start + HistoryPeriod.Months(1).ToNodaPeriod());
    }
}
