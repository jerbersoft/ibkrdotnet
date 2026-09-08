using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.Scanner;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class ScannerClientTests
{
    [Fact]
    public async Task Reads_the_documented_parameters_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-parameters.success.json");
        var client = new ScannerClient(harness.ApiClient);

        var parameters = await client.GetParametersAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/scanner/params", harness.LastRequest.Path);

        var scanType = Assert.Single(parameters.ScanTypes);
        Assert.Equal("TOP_PERC_GAIN", scanType.Code);
        Assert.Equal("Top % Gainers", scanType.DisplayName);
        Assert.Contains("STK", scanType.Instruments);

        var instrument = Assert.Single(parameters.Instruments);
        Assert.Equal("STK", instrument.Type);
        Assert.Equal(3, instrument.Filters.Count);

        Assert.Equal(2, parameters.Filters.Count);
        Assert.Equal("non-range", parameters.Filters[0].Type);
        Assert.True(Assert.Single(parameters.Filters[1].ComboValues).IsDefault);
    }

    [Fact]
    public async Task Reads_a_location_tree_whose_leaves_omit_their_children()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-parameters.live.json");
        var client = new ScannerClient(harness.ApiClient);

        var parameters = await client.GetParametersAsync(TestContext.Current.CancellationToken);

        var root = Assert.Single(parameters.Locations);
        Assert.Equal("STK", root.Type);
        Assert.Equal(2, root.Locations.Count);

        // A leaf sends no 'locations' key at all rather than an empty array.
        Assert.Empty(root.Locations[0].Locations);
        Assert.Equal("STK.US.MAJOR", root.Locations[0].Type);
    }

    [Fact]
    public async Task Reads_a_combo_filter_that_offers_no_values_to_choose_from()
    {
        // A live gateway sends combo choices carrying nothing but which one is the default, so a
        // combo filter's permissible values are not discoverable from this endpoint.
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-parameters.live.json");
        var client = new ScannerClient(harness.ApiClient);

        var parameters = await client.GetParametersAsync(TestContext.Current.CancellationToken);

        var combo = Assert.Single(parameters.Filters, f => f.Type == "combo");
        Assert.Equal("haltedIs", combo.Code);
        Assert.Equal(3, combo.ComboValues.Count);
        Assert.Equal([true, false, false], combo.ComboValues.Select(v => v.IsDefault));
    }

    [Fact]
    public async Task Sends_the_scan_ibkr_expects_and_reads_its_results()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-results.success.json");
        var client = new ScannerClient(harness.ApiClient);

        var results = await client.RunAsync(
            "STK",
            "TOP_TRADE_COUNT",
            "STK.US.MAJOR",
            [new ScannerFilter("priceAbove", 10)],
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/scanner/run", harness.LastRequest.Path);
        Assert.Equal(
            """{"instrument":"STK","type":"TOP_TRADE_COUNT","location":"STK.US.MAJOR","filter":[{"code":"priceAbove","value":10}]}""",
            harness.LastRequest.Body);

        Assert.Equal("Trades", results.ScanDataColumnName);
        Assert.Equal(2, results.Contracts.Count);
        Assert.Equal(new ConId(76792991), results.Contracts[0].ConId);
        Assert.Equal("TSLA", results.Contracts[0].Symbol);
        Assert.Equal("221.521K", results.Contracts[0].ScanData);

        // IBKR sends the column heading on the first row only.
        Assert.Equal("Trades", results.Contracts[0].ColumnName);
        Assert.Null(results.Contracts[1].ColumnName);
    }

    [Fact]
    public async Task Omits_a_location_that_was_not_given()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-results.success.json");
        var client = new ScannerClient(harness.ApiClient);

        await client.RunAsync("STK", "TOP_PERC_GAIN", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            """{"instrument":"STK","type":"TOP_PERC_GAIN","filter":[]}""",
            harness.LastRequest.Body);
    }

    [Fact]
    public async Task Reads_a_scan_that_returned_no_ranking_value()
    {
        // IBKR's published example carries 'scan_data' on every row, but a live gateway omitted it
        // from all fifty rows of a Top % Gainers scan, run both before the open and during regular
        // trading hours, returning only the column heading.
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-scanner/get-scanner-results.live.json");
        var client = new ScannerClient(harness.ApiClient);

        var results = await client.RunAsync(
            "STK", "TOP_PERC_GAIN", "STK.US.MAJOR", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Chg%", results.ScanDataColumnName);
        Assert.Equal(3, results.Contracts.Count);
        Assert.All(results.Contracts, c => Assert.Null(c.ScanData));
        Assert.Equal("PDSB", results.Contracts[0].Symbol);
        Assert.Equal(new ConId(357194949), results.Contracts[0].ConId);
    }

    [Theory]
    [InlineData("", "TOP_PERC_GAIN")]
    [InlineData("STK", "")]
    [InlineData("   ", "TOP_PERC_GAIN")]
    public async Task Refuses_a_scan_missing_an_instrument_or_a_type(string instrument, string scanType)
    {
        using var harness = new ClientHarness();
        var client = new ScannerClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RunAsync(instrument, scanType, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Stub.Requests);
    }
}
