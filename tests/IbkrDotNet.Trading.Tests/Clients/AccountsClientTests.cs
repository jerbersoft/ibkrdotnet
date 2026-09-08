using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class AccountsClientTests
{
    private static readonly AccountId Account = new("U1234567");

    [Fact]
    public async Task Reads_the_documented_tradable_accounts_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-brokerage-accounts.json");
        var client = new AccountsClient(harness.ApiClient);

        var accounts = await client.GetTradableAccountsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/accounts", harness.LastRequest.Path);
        Assert.Equal([Account], accounts.Accounts);
        Assert.True(accounts.AccountProperties?["U1234567"].SupportsFractions);
        Assert.False(accounts.AccountProperties?["U1234567"].HasChildAccounts);
    }

    [Fact]
    public async Task Reads_the_documented_account_summary_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-account-summary.json");
        var client = new AccountsClient(harness.ApiClient);

        var summary = await client.GetSummaryAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/U1234567/summary", harness.LastRequest.Path);
        Assert.Equal(1290490m, summary.NetLiquidationValue);
        Assert.Equal(-401846m, summary.TotalCashValue);
        Assert.Contains(summary.CashBalances, b => b.Currency == "EUR" && b.Balance == 194m);
    }

    [Theory]
    [InlineData("balances")]
    [InlineData("margins")]
    [InlineData("market_value")]
    [InlineData("available_funds")]
    public async Task Targets_the_right_segment_summary_endpoint(string segment)
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("{}");
        var client = new AccountsClient(harness.ApiClient);

        _ = segment switch
        {
            "balances" => await client.GetBalanceSummaryAsync(Account, TestContext.Current.CancellationToken),
            "margins" => await client.GetMarginSummaryAsync(Account, TestContext.Current.CancellationToken),
            "market_value" => await client.GetMarketValueSummaryAsync(Account, TestContext.Current.CancellationToken),
            _ => await client.GetAvailableFundsSummaryAsync(Account, TestContext.Current.CancellationToken),
        };

        Assert.Equal($"/v1/api/iserver/account/U1234567/summary/{segment}", harness.LastRequest.Path);
    }

    [Fact]
    public async Task Exposes_the_documented_balance_segments_and_parses_their_display_values()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-balance-summary.json");
        var client = new AccountsClient(harness.ApiClient);

        var summary = await client.GetBalanceSummaryAsync(Account, TestContext.Current.CancellationToken);

        Assert.NotNull(summary.Total);
        Assert.NotNull(summary.Securities);
        Assert.NotNull(summary.Commodities);

        // IBKR mixes stable keys with abbreviated display labels in the same map.
        Assert.Equal("1,288,301 USD", summary.Total!["net_liquidation"]);
        Assert.Equal("0 USD", summary.Total["Nt Lqdtn Uncrtnty"]);

        var netLiquidation = summary.GetAmount("total", "net_liquidation");
        Assert.Equal(1288301m, netLiquidation?.Amount);
        Assert.Equal("USD", netLiquidation?.Currency);
    }

    [Fact]
    public async Task Keys_the_market_value_summary_by_currency()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-account-market-summary.json");
        var client = new AccountsClient(harness.ApiClient);

        var summary = await client.GetMarketValueSummaryAsync(Account, TestContext.Current.CancellationToken);

        Assert.Contains("EUR", summary.Segments.Keys);
        Assert.Equal(194m, summary.GetAmount("EUR", "net_liquidation")?.Amount);
    }

    [Fact]
    public async Task Returns_null_for_the_non_numeric_placeholders_ibkr_mixes_in()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-fund-summary.json");
        var client = new AccountsClient(harness.ApiClient);

        var summary = await client.GetAvailableFundsSummaryAsync(Account, TestContext.Current.CancellationToken);

        // 'leverage' is "n/a", 'day_trades_left' is "Unlimited" and 'Lk Ahd Nxt Chng' is "@ 16:00:00".
        Assert.Null(summary.GetAmount("total", "leverage"));
        Assert.Null(summary.GetAmount("total", "day_trades_left"));
        Assert.Null(summary.GetAmount("total", "Lk Ahd Nxt Chng"));
        Assert.Equal(3304346m, summary.GetAmount("total", "buying_power")?.Amount);
    }

    [Fact]
    public async Task Reads_the_documented_partitioned_pnl_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-pnl.json");
        var client = new AccountsClient(harness.ApiClient);

        var pnl = await client.GetProfitAndLossAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/pnl/partitioned", harness.LastRequest.Path);
        var core = pnl.Partitions["U1234567.Core"];
        Assert.Equal(-12510m, core.DailyPnl);
        Assert.Equal(1290000m, core.NetLiquidity);
    }

    [Fact]
    public async Task Reads_the_documented_dynamic_account_search_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-dynamic-accounts.json");
        var client = new AccountsClient(harness.ApiClient);

        var result = await client.SearchDynamicAccountsAsync("U1", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/search/U1", harness.LastRequest.Path);
        Assert.Equal(Account, result.MatchedAccounts[0].AccountId);
    }

    [Fact]
    public async Task Reads_the_documented_owners_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/get-account-owners.json");
        var client = new AccountsClient(harness.ApiClient);

        var owners = await client.GetOwnersAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/acesws/U1234567/signatures-and-owners", harness.LastRequest.Path);
        Assert.Equal("OWNER", owners.Users[0].RoleId);
        Assert.Equal("John Smith", owners.Users[0].Entity?.EntityName);
        Assert.Equal(["John Smith"], owners.Applicant?.Signatures);
    }

    [Fact]
    public async Task Posts_the_account_identifier_when_switching_accounts()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/set-active-account.json");
        var client = new AccountsClient(harness.ApiClient);

        var result = await client.SwitchAccountAsync(new AccountId("U2234567"), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account", harness.LastRequest.Path);
        Assert.Equal("""{"acctId":"U2234567"}""", harness.LastRequest.Body);
        Assert.True(result.Set);
        Assert.Equal(new AccountId("U2234567"), result.AccountId);
    }

    [Fact]
    public async Task Posts_the_account_identifier_when_setting_the_dynamic_account()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-accounts/set-dynamic-account.json");
        var client = new AccountsClient(harness.ApiClient);

        await client.SetDynamicAccountAsync(new AccountId("U2234567"), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/dynaccount", harness.LastRequest.Path);
        Assert.Equal("""{"acctId":"U2234567"}""", harness.LastRequest.Body);
    }
}

public class MonetaryValueTests
{
    [Theory]
    [InlineData("1,288,301 USD", 1288301, "USD")]
    [InlineData("-401,693 USD", -401693, "USD")]
    [InlineData("0 USD", 0, "USD")]
    [InlineData("194", 194, null)]
    [InlineData("1.092525", 1.092525, null)]
    public void Parses_the_display_formats_ibkr_returns(string input, decimal amount, string? currency)
    {
        Assert.True(MonetaryValue.TryParse(input, out var value));
        Assert.Equal(amount, value.Amount);
        Assert.Equal(currency, value.Currency);
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("Unlimited")]
    [InlineData("@ 16:00:00")]
    [InlineData("")]
    [InlineData(null)]
    public void Reports_failure_for_the_placeholders_rather_than_guessing_a_number(string? input)
    {
        Assert.False(MonetaryValue.TryParse(input, out _));
    }
}
