using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.EventContracts;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class EventContractsClientTests
{
    private const string Fixtures = "trading-event-contracts/";

    [Fact]
    public async Task Reads_the_documented_category_tree()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-categories.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var tree = await client.GetCategoryTreeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/forecast/category/tree", harness.LastRequest.Path);
        Assert.Equal(3, tree.Categories.Count);

        var withMarkets = tree.Categories["g17490"];
        Assert.Equal("United States", withMarkets.Name);
        Assert.Equal("g7369", withMarkets.ParentId);
        Assert.Equal(2, withMarkets.Markets.Count);

        var market = withMarkets.Markets[0];
        Assert.Equal("United States Carbon Dioxide Emissions", market.Name);
        Assert.Equal("USCE", market.Symbol);
        Assert.Equal("FORECASTX", market.Exchange);
        Assert.Equal(new ConId(732764706), market.ConId);
        Assert.Equal(new ConId(732764711), market.ProductConId);

        // A root has no parent, and a grouping category carries no markets.
        Assert.Null(tree.Categories["g5351"].ParentId);
        Assert.Empty(tree.Categories["g5351"].Markets);
    }

    [Fact]
    public async Task Reads_the_documented_market()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-markets.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var market = await client.GetMarketAsync(
            new ConId(732764706), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/forecast/contract/market", harness.LastRequest.Path);
        Assert.Equal("?underlyingConid=732764706", harness.LastRequest.Query);

        Assert.Equal("United States Carbon Dioxide Emissions", market.MarketName);
        Assert.False(market.ExcludeHistoricalData);
        Assert.Equal(1m, market.Payout);
        Assert.Equal(2, market.Contracts.Count);

        var yes = market.Contracts[0];
        Assert.Equal(new ConId(732957192), yes.ConId);
        Assert.Equal(EventContractSide.Yes, yes.Side);
        Assert.Equal(new LocalDate(2026, 4, 30), yes.Expiration);
        Assert.Equal(4700m, yes.Strike);
        Assert.Equal("Above 4,700", yes.StrikeLabel);
        Assert.Equal("2025.12.31", yes.TimeSpecifier);

        Assert.Equal(EventContractSide.No, market.Contracts[1].Side);
    }

    [Fact]
    public async Task Sends_the_exchange_only_when_one_is_given()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-markets.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        await client.GetMarketAsync(
            new ConId(732764706), "FORECASTX", TestContext.Current.CancellationToken);

        Assert.Equal(
            "?underlyingConid=732764706&exchange=FORECASTX", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Reads_the_documented_contract_details()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-contract.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var details = await client.GetDetailsAsync(
            new ConId(805953033), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/forecast/contract/details", harness.LastRequest.Path);
        Assert.Equal("?conid=805953033", harness.LastRequest.Query);

        Assert.Equal(new ConId(805953033), details.YesConId);
        Assert.Equal(new ConId(805953036), details.NoConId);
        Assert.StartsWith("Will US carbon dioxide emmissions", details.Question, StringComparison.Ordinal);
        Assert.Equal(EventContractSide.Yes, details.Side);
        Assert.Equal(5050m, details.Strike);
        Assert.Equal(new LocalDate(2036, 4, 30), details.Expiration);
        Assert.Equal("g5351", details.Category);
        Assert.Equal(1m, details.Payout);
    }

    [Fact]
    public async Task Reads_the_documented_rules()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-rules.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var rules = await client.GetRulesAsync(
            new ConId(805953033), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/forecast/contract/rules", harness.LastRequest.Path);

        Assert.Equal("OPT", rules.AssetClass);
        Assert.Equal("Energy Information Agency", rules.SourceAgency);
        Assert.Equal("5050.0", rules.Threshold);
        Assert.Equal(Instant.FromUnixTimeSeconds(2093202000), rules.LastTradeTime);
        Assert.Equal(Instant.FromUnixTimeSeconds(2093277600), rules.PayoutTime);

        // The rules endpoint's 'payout' is a formatted string, unlike the numeric field of the same
        // name on the market and the contract.
        Assert.Equal("$1.00", rules.Payout);
        Assert.Equal("$0.01", rules.PriceIncrement);
    }

    [Fact]
    public async Task Reads_the_documented_schedule()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-schedule.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var schedule = await client.GetScheduleAsync(
            new ConId(805953033), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/forecast/contract/schedules", harness.LastRequest.Path);
        Assert.Equal(3, schedule.TradingDays.Count);

        var saturday = schedule.TradingDays[0];
        Assert.Equal(IsoDayOfWeek.Saturday, saturday.DayOfWeek);
        Assert.Equal(2, saturday.TradingTimes.Count);
        Assert.Equal(new LocalTime(0, 0), saturday.TradingTimes[0].Open);
        Assert.Equal(new LocalTime(16, 0), saturday.TradingTimes[0].Close);
        Assert.Equal(new LocalTime(16, 15), saturday.TradingTimes[1].Open);
        Assert.Equal(new LocalTime(23, 59), saturday.TradingTimes[1].Close);
    }

    [Fact]
    public async Task Resolves_the_tzdb_alias_ibkr_sends_for_the_exchange_zone()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-schedule.documented.json");
        var client = new EventContractsClient(harness.ApiClient);

        var schedule = await client.GetScheduleAsync(
            new ConId(805953033), TestContext.Current.CancellationToken);

        // IBKR sends "US/Central", which is a TZDB link to America/Chicago rather than the canonical
        // identifier. The zone keeps the id it was asked for, so compare behaviour, not the name.
        Assert.NotNull(schedule.TimeZone);
        Assert.Equal("US/Central", schedule.TimeZone.Id);

        var noon = new LocalDateTime(2026, 1, 15, 12, 0);
        Assert.Equal(
            noon.InZoneLeniently(DateTimeZoneProviders.Tzdb["America/Chicago"]).ToInstant(),
            noon.InZoneLeniently(schedule.TimeZone).ToInstant());
    }

    [Fact]
    public async Task Rejects_a_day_of_week_it_does_not_recognise()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(
            """{"timezone":"US/Central","trading_schedules":[{"day_of_week":"Caturday"}]}""");
        var client = new EventContractsClient(harness.ApiClient);

        // Unlike the notification type code, this set genuinely cannot grow, so an unknown value is
        // a failure rather than something to carry through.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.GetScheduleAsync(new ConId(1), TestContext.Current.CancellationToken));
    }

    // ---- Captured from a live gateway ------------------------------------------------------------
    //
    // The fixtures below end '.live.json'. Every one of them carries a field IBKR's reference does
    // not mention, or a type it contradicts: 'is_restricted' on every category and market, 'party'
    // and 'product_conid' on a contract, a 'price_increments' array beside the scalar increment, and
    // a strike documented as an integer that arrives as 1.0.

    [Fact]
    public async Task Reads_the_three_category_shapes_a_live_tree_mixes()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-categories.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var tree = await client.GetCategoryTreeAsync(TestContext.Current.CancellationToken);

        // A root: no parent, and an empty markets array.
        var root = tree.Categories["g137821"];
        Assert.Equal("Elections", root.Name);
        Assert.Null(root.ParentId);
        Assert.Empty(root.Markets);

        // A grouping level: a parent, and no markets key at all. IBKR uses both spellings of "none".
        var grouping = tree.Categories["g137914"];
        Assert.Equal("United States Gubernatorial", grouping.Name);
        Assert.Equal("g137821", grouping.ParentId);
        Assert.Empty(grouping.Markets);

        // A leaf: markets, each with a market conid and a distinct product conid.
        var leaf = tree.Categories["g137940"];
        Assert.Equal("g137914", leaf.ParentId);
        var market = Assert.Single(leaf.Markets);
        Assert.Equal("Illinois Governor General Election", market.Name);
        Assert.Equal(new ConId(848769488), market.ConId);
        Assert.Equal(new ConId(848769493), market.ProductConId);
        Assert.NotEqual(market.ConId, market.ProductConId);
    }

    [Fact]
    public async Task Carries_through_the_undocumented_restriction_flag()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-categories.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var tree = await client.GetCategoryTreeAsync(TestContext.Current.CancellationToken);

        Assert.False(tree.Categories["g137821"].IsRestricted);
        Assert.False(Assert.Single(tree.Categories["g137940"].Markets).IsRestricted);
    }

    [Fact]
    public async Task Reads_a_live_strike_that_is_fractional_where_the_docs_promise_an_integer()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-markets.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var market = await client.GetMarketAsync(
            new ConId(848769488), cancellationToken: TestContext.Current.CancellationToken);

        // On a market whose outcomes are named rather than numeric, the strike is an ordinal and the
        // label is the part that means anything.
        var first = market.Contracts[0];
        Assert.Equal(1m, first.Strike);
        Assert.Equal("JB Pritzker", first.StrikeLabel);
        Assert.Equal("Democrat", first.Party);

        // Both sides of the same strike are listed, with different conids.
        Assert.Equal(EventContractSide.Yes, market.Contracts[0].Side);
        Assert.Equal(EventContractSide.No, market.Contracts[1].Side);
        Assert.Equal(market.Contracts[0].StrikeLabel, market.Contracts[1].StrikeLabel);
        Assert.NotEqual(market.Contracts[0].ConId, market.Contracts[1].ConId);
    }

    [Fact]
    public async Task Reads_the_undocumented_fields_a_live_contract_carries()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-contract.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var details = await client.GetDetailsAsync(
            new ConId(849139254), TestContext.Current.CancellationToken);

        Assert.Equal(new ConId(848769493), details.ProductConId);
        Assert.Equal("Democrat", details.Party);
        Assert.False(details.IsRestricted);

        // The pair, from either side's conid.
        Assert.Equal(new ConId(849139254), details.YesConId);
        Assert.Equal(new ConId(849139257), details.NoConId);
    }

    [Fact]
    public async Task Reads_the_price_increment_table_the_docs_omit()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-rules.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var rules = await client.GetRulesAsync(
            new ConId(849139254), TestContext.Current.CancellationToken);

        var increment = Assert.Single(rules.PriceIncrements);
        Assert.Equal("0.0", increment.LowerEdge);
        Assert.Equal("$0.01", increment.Increment);

        // An election settles on a name, so the threshold is not a number at all.
        Assert.Equal("JB Pritzker", rules.Threshold);
        Assert.Equal("Illinois State Board of Elections", rules.SourceAgency);
        Assert.NotNull(rules.ExchangeTimeZone);
    }

    [Fact]
    public async Task Reads_a_live_schedule_split_by_a_one_minute_closure()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-forecast-schedule.live.json");
        var client = new EventContractsClient(harness.ApiClient);

        var schedule = await client.GetScheduleAsync(
            new ConId(849139254), TestContext.Current.CancellationToken);

        Assert.Equal(7, schedule.TradingDays.Count);
        Assert.Equal(IsoDayOfWeek.Saturday, schedule.TradingDays[0].DayOfWeek);

        // The break is 4:15 PM to 4:16 PM, so a day is two blocks rather than one.
        var day = schedule.TradingDays[0];
        Assert.Equal(new LocalTime(16, 15), day.TradingTimes[0].Close);
        Assert.Equal(new LocalTime(16, 16), day.TradingTimes[1].Open);
        Assert.Equal(new LocalTime(23, 59), day.TradingTimes[1].Close);
    }
}
