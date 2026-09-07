using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Time;

public class BarSizeTests
{
    [Theory]
    [InlineData(1, IbkrTimeUnit.Seconds, "1S")]
    [InlineData(30, IbkrTimeUnit.Seconds, "30S")]
    [InlineData(1, IbkrTimeUnit.Minutes, "1min")]
    [InlineData(5, IbkrTimeUnit.Minutes, "5min")]
    [InlineData(1, IbkrTimeUnit.Hours, "1h")]
    [InlineData(1, IbkrTimeUnit.Days, "1d")]
    [InlineData(1, IbkrTimeUnit.Weeks, "1w")]
    [InlineData(1, IbkrTimeUnit.Months, "1m")]
    public void Formats_to_the_wire_representation(int count, IbkrTimeUnit unit, string expected)
    {
        Assert.Equal(expected, new BarSize(count, unit).ToString());
    }

    [Theory]
    [InlineData("1S")]
    [InlineData("30S")]
    [InlineData("1min")]
    [InlineData("5min")]
    [InlineData("1h")]
    [InlineData("1d")]
    [InlineData("1w")]
    [InlineData("1m")]
    public void Round_trips_through_parsing(string wire)
    {
        Assert.Equal(wire, BarSize.Parse(wire).ToString());
    }

    [Fact]
    public void Distinguishes_months_from_minutes_by_case()
    {
        // 'm' is months and 'min' is minutes; conflating them would silently request 30x the data.
        Assert.Equal(IbkrTimeUnit.Months, BarSize.Parse("1m").Unit);
        Assert.Equal(IbkrTimeUnit.Minutes, BarSize.Parse("1min").Unit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("min")]
    [InlineData("0d")]
    [InlineData("-1d")]
    [InlineData("1y")]      // years are valid as a period but not as a bar width
    [InlineData("1fortnight")]
    public void Rejects_values_ibkr_would_not_accept(string wire)
    {
        Assert.False(BarSize.TryParse(wire, out _));
    }

    [Fact]
    public void Rejects_a_years_unit_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarSize(1, IbkrTimeUnit.Years));
    }

    [Theory]
    [InlineData(30, IbkrTimeUnit.Seconds, 30)]
    [InlineData(5, IbkrTimeUnit.Minutes, 300)]
    [InlineData(2, IbkrTimeUnit.Hours, 7200)]
    [InlineData(1, IbkrTimeUnit.Days, 86400)]
    [InlineData(1, IbkrTimeUnit.Weeks, 604800)]
    public void Converts_to_an_exact_duration_where_one_exists(int count, IbkrTimeUnit unit, long seconds)
    {
        Assert.Equal(Duration.FromSeconds(seconds), new BarSize(count, unit).ToDuration());
    }

    [Fact]
    public void Has_no_duration_for_months_because_months_have_no_fixed_length()
    {
        Assert.Null(BarSize.OneMonth.ToDuration());
    }
}
