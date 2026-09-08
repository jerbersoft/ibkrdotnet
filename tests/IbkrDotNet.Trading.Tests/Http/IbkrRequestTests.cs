using IbkrDotNet.Trading.Http;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

public class IbkrRequestTests
{
    [Fact]
    public void Renders_a_path_with_no_query()
    {
        Assert.Equal("/v1/api/iserver/accounts", IbkrRequest.Get("/v1/api/iserver/accounts").ToRelativeUri());
    }

    [Fact]
    public void Renders_query_parameters_in_insertion_order()
    {
        var request = IbkrRequest.Get("/v1/api/iserver/marketdata/snapshot")
            .WithQuery("conids", "265598,8314")
            .WithQuery("fields", "31,84,86");

        Assert.Equal(
            "/v1/api/iserver/marketdata/snapshot?conids=265598%2C8314&fields=31%2C84%2C86",
            request.ToRelativeUri());
    }

    [Fact]
    public void Omits_null_query_parameters_rather_than_sending_them_empty()
    {
        var request = IbkrRequest.Get("/v1/api/iserver/account/trades")
            .WithQuery("days", (long?)null)
            .WithQuery("accountId", (string?)null);

        Assert.Equal("/v1/api/iserver/account/trades", request.ToRelativeUri());
        Assert.Empty(request.Query);
    }

    [Fact]
    public void Renders_booleans_the_way_ibkr_expects()
    {
        var request = IbkrRequest.Get("/v1/api/iserver/marketdata/history").WithQuery("outsideRth", true);

        Assert.Equal("true", request.Query["outsideRth"]);
    }

    [Fact]
    public void Escapes_path_segments_so_an_identifier_cannot_alter_the_route()
    {
        var segment = IbkrRequest.PathSegment("U123/../../logout");

        Assert.DoesNotContain("/", segment, StringComparison.Ordinal);
    }

    [Fact]
    public void Joins_comma_separated_values_and_skips_an_empty_sequence()
    {
        var withValues = IbkrRequest.Get("/v1/api/x").WithCommaSeparatedQuery("conids", ["1", "2"]);
        var withNone = IbkrRequest.Get("/v1/api/x").WithCommaSeparatedQuery("conids", []);

        Assert.Equal("1,2", withValues.Query["conids"]);
        Assert.Empty(withNone.Query);
    }

    [Fact]
    public void Rejects_a_path_that_is_not_rooted()
    {
        Assert.Throws<ArgumentException>(() => IbkrRequest.Get("iserver/accounts"));
    }
}
