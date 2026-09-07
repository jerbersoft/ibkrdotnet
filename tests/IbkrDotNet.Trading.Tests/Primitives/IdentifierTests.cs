using IbkrDotNet.Trading.Primitives;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Primitives;

public class IdentifierTests
{
    [Fact]
    public void ConId_parses_and_formats_invariantly()
    {
        Assert.Equal(new ConId(265598), ConId.Parse("265598"));
        Assert.Equal("265598", new ConId(265598).ToString());
    }

    [Fact]
    public void ConId_orders_numerically()
    {
        Assert.True(new ConId(8314) < new ConId(265598));
        Assert.True(new ConId(265598) >= new ConId(265598));
    }

    [Fact]
    public void ConId_rejects_text_that_is_not_a_contract_identifier()
    {
        Assert.Throws<FormatException>(() => ConId.Parse("AAPL"));
        Assert.False(ConId.TryParse(null, out _));
    }

    [Fact]
    public void AccountId_compares_ordinally_and_case_sensitively()
    {
        Assert.Equal(new AccountId("U1234567"), new AccountId("U1234567"));
        Assert.NotEqual(new AccountId("U1234567"), new AccountId("u1234567"));
    }

    [Fact]
    public void AccountId_rejects_an_empty_identifier()
    {
        Assert.Throws<ArgumentException>(() => new AccountId("   "));
    }

    [Fact]
    public void OrderId_parses_and_formats_invariantly()
    {
        Assert.Equal(new OrderId(987654), OrderId.Parse("987654"));
        Assert.Equal("987654", new OrderId(987654).ToString());
    }
}
