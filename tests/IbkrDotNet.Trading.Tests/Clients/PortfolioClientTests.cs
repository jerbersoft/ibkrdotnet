using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class PortfolioClientTests
{
    private static readonly AccountId Account = new("DU123456");

    [Fact]
    public async Task Reads_the_documented_accounts_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-all-accounts.json");
        var client = new PortfolioClient(harness.ApiClient);

        var accounts = await client.GetAccountsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/accounts", harness.LastRequest.Path);
        Assert.Equal(Account, accounts[0].AccountId);
        Assert.Equal("John Smith, LLC", accounts[0].AccountTitle);

        // 'accountStatus' is the account opening time in epoch milliseconds, not a status code.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1590724800000), accounts[0].OpenedAt);
    }

    [Fact]
    public async Task Reads_the_documented_subaccounts_page()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-many-subaccounts.json");
        var client = new PortfolioClient(harness.ApiClient);

        var page = await client.GetSubaccountsPageAsync(0, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/subaccounts2", harness.LastRequest.Path);
        Assert.Equal("?page=0", harness.LastRequest.Query);
        Assert.Equal(1, page.Metadata?.Total);
        Assert.Equal(new AccountId("U1234567"), page.Subaccounts[0].AccountId);
    }

    [Fact]
    public async Task Reads_the_documented_account_metadata_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-portfolio-metadata.json");
        var client = new PortfolioClient(harness.ApiClient);

        var metadata = await client.GetAccountMetadataAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/meta", harness.LastRequest.Path);
        Assert.Equal("Retirement", metadata.AccountAlias);
        Assert.Equal("STKNOPT", metadata.TradingType);
        Assert.False(metadata.Parent?.IsMultiplex);
    }

    [Fact]
    public async Task Reads_the_documented_summary_payload_with_its_mixed_numeric_and_textual_entries()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-portfolio-summary.json");
        var client = new PortfolioClient(harness.ApiClient);

        var summary = await client.GetSummaryAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/summary", harness.LastRequest.Path);

        // Textual entries carry 'value'; numeric entries carry 'amount'.
        Assert.Equal("DU123456", summary["accountcode"].Value);
        Assert.Equal(880036.375m, summary["accruedcash"].Amount);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1712156105000), summary["accountcode"].Timestamp);
    }

    [Fact]
    public async Task Reads_the_documented_ledger_payload_keyed_by_currency()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-portfolio-ledger.json");
        var client = new PortfolioClient(harness.ApiClient);

        var ledger = await client.GetLedgerAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/ledger", harness.LastRequest.Path);
        Assert.Contains("BASE", ledger.Keys);
        Assert.Equal(223911.11m, ledger["AUD"].CashBalance);
        Assert.Equal(0.650378m, ledger["AUD"].ExchangeRate);

        // The ledger timestamp is epoch seconds, unlike most other IBKR timestamps.
        Assert.Equal(Instant.FromUnixTimeSeconds(1754948718), ledger["AUD"].RetrievedAt);
    }

    [Fact]
    public async Task Reads_the_documented_allocation_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-asset-allocation.json");
        var client = new PortfolioClient(harness.ApiClient);

        var allocation = await client.GetAllocationAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/allocation", harness.LastRequest.Path);
        Assert.Equal(380106.54m, allocation.AssetClass?.LongPositions["BOND"]);
        Assert.Equal(-103716.11109948158m, allocation.AssetClass?.ShortPositions["STK"]);
        Assert.Contains("Aerospace/Defense", allocation.Group!.LongPositions.Keys);
    }

    [Fact]
    public async Task Reads_the_documented_positions_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-paginated-positions.json");
        var client = new PortfolioClient(harness.ApiClient);

        var positions = await client.GetPositionsAsync(Account, 0, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/positions/0", harness.LastRequest.Path);
        Assert.Equal(new ConId(479624278), positions[0].ConId);
        Assert.Equal("CRYPTO", positions[0].AssetClass);
        Assert.Equal(27608.34921045m, positions[0].AverageCost);

        // A crypto position has no expiry, and a null must survive the LocalDate converter.
        Assert.Null(positions[0].Expiry);
        Assert.Equal(0.25m, positions[0].IncrementRules[0].Increment);
    }

    [Fact]
    public async Task Reads_the_documented_positions_by_instrument_payload_keyed_by_account()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-all-accounts-for-conid.json");
        var client = new PortfolioClient(harness.ApiClient);

        var byAccount = await client.GetPositionsByInstrumentAsync(
            new ConId(265598),
            TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/positions/265598", harness.LastRequest.Path);
        Assert.Equal(Account, byAccount["DU123456"][0].AccountId);
    }

    [Fact]
    public async Task Reads_the_documented_combo_positions_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-combo-positions.json");
        var client = new PortfolioClient(harness.ApiClient);

        var combos = await client.GetComboPositionsAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio/DU123456/combo/positions", harness.LastRequest.Path);
        Assert.Equal("1*649180695-1*654503299", combos[0].Description);

        // A short leg is expressed as a negative ratio, and legs quote conids as strings.
        Assert.Equal(new ConId(649180695), combos[0].Legs[0].ConId);
        Assert.Equal(-1m, combos[0].Legs[1].Ratio);
    }

    [Fact]
    public async Task Reads_the_documented_uncached_positions_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/get-uncached-positions.json");
        var client = new PortfolioClient(harness.ApiClient);

        var positions = await client.GetUncachedPositionsAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/portfolio2/DU123456/positions", harness.LastRequest.Path);
        Assert.Equal(new ConId(265598), positions[0].ConId);
        Assert.Equal("AAPL", positions[0].Description);

        // This endpoint reports its timestamp in epoch seconds.
        Assert.Equal(Instant.FromUnixTimeSeconds(1767885880), positions[0].RetrievedAt);
    }

    [Fact]
    public async Task Posts_to_invalidate_the_position_cache()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-portfolio/invalidate-position-cache.json");
        var client = new PortfolioClient(harness.ApiClient);

        var result = await client.InvalidatePositionCacheAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/portfolio/DU123456/positions/invalidate", harness.LastRequest.Path);
        Assert.Equal("success", result.Message);
    }
}
