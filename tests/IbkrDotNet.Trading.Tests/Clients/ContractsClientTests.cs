using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class ContractsClientTests
{
    private static readonly ConId Ibm = new(8314);

    [Fact]
    public async Task Reads_the_documented_search_payload_and_splits_the_expiry_lists()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-contract-symbols.json");
        var client = new ContractsClient(harness.ApiClient);

        var results = await client.SearchAsync("IBM", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/secdef/search", harness.LastRequest.Path);
        Assert.Equal("?symbol=IBM", harness.LastRequest.Query);
        Assert.Equal(Ibm, results[0].ConId);

        // IBKR packs option expiries into one semicolon-delimited string.
        Assert.Contains(new LocalDate(2024, 3, 15), results[0].OptionExpiries);
        Assert.Empty(results[0].FuturesOptionExpiries);
    }

    [Fact]
    public async Task Reads_the_documented_instrument_attributes_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson($"[{Fixture.ReadText("Responses/trading-contracts/get-contract-info.json")}]");
        var client = new ContractsClient(harness.ApiClient);

        var attributes = await client.GetAttributesAsync(Ibm, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/secdef/info", harness.LastRequest.Path);
        Assert.Equal("?conid=8314", harness.LastRequest.Query);
        Assert.Equal("IBM", attributes[0].Ticker);

        // 'strike' is documented as a string and arrives as a number.
        Assert.Equal("0", attributes[0].Strike);
    }

    [Fact]
    public async Task Reads_the_documented_strikes_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-contract-strikes.json");
        var client = new ContractsClient(harness.ApiClient);

        var strikes = await client.GetStrikesAsync(Ibm, "OPT", "JAN25", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/secdef/strikes", harness.LastRequest.Path);
        Assert.Contains("sectype=OPT", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Equal(70m, strikes.Call[0]);
        Assert.Equal(100m, strikes.Put[^1]);
    }

    [Fact]
    public async Task Reads_the_documented_info_and_rules_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-info-and-rules.json");
        var client = new ContractsClient(harness.ApiClient);

        var info = await client.GetInfoAndRulesAsync(Ibm, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/contract/8314/info-and-rules", harness.LastRequest.Path);
        Assert.Equal(Ibm, info.ConId);
        Assert.Equal("Computers", info.Industry);
        Assert.NotNull(info.Rules);
    }

    [Fact]
    public async Task Posts_the_documented_body_when_asking_for_contract_rules()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-contract-rules.json");
        var client = new ContractsClient(harness.ApiClient);

        var rules = await client.GetRulesAsync(Ibm, isBuy: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/contract/rules", harness.LastRequest.Path);
        Assert.Equal("""{"conid":8314,"isBuy":false,"modifyOrder":false}""", harness.LastRequest.Body);
        Assert.True(rules.AlgoEligible);
        Assert.Contains("limit", rules.OrderTypes);
        Assert.Contains(new AccountId("U1234567"), rules.TradableAccountIds);
    }

    [Fact]
    public async Task Sends_algorithm_ids_semicolon_delimited_as_ibkr_requires()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-algos-by-instrument.json");
        var client = new ContractsClient(harness.ApiClient);

        var algos = await client.GetAlgorithmsAsync(
            Ibm,
            ["Adaptive", "ArrivalPx"],
            includeParameters: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/contract/8314/algos", harness.LastRequest.Path);
        Assert.Contains("algos=Adaptive%3BArrivalPx", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("addParams=1", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Equal("Adaptive", algos.Algorithms[0].Id);
    }

    [Fact]
    public async Task Reads_the_documented_instrument_definitions_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-instrument-definition.json");
        var client = new ContractsClient(harness.ApiClient);

        var definitions = await client.GetDefinitionsAsync([Ibm], TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/trsrv/secdef", harness.LastRequest.Path);
        Assert.Equal("?conids=8314", harness.LastRequest.Query);
        Assert.Equal(Ibm, definitions.Definitions[0].ConId);

        // Here displayRule is a list, though the same concept is a single object on a position.
        Assert.Single(definitions.Definitions[0].DisplayRules);
        Assert.Equal(0.01m, definitions.Definitions[0].IncrementRules[0].Increment);
    }

    [Fact]
    public async Task Refuses_more_conids_than_ibkr_accepts_rather_than_truncating()
    {
        using var harness = new ClientHarness();
        var client = new ContractsClient(harness.ApiClient);
        var tooMany = Enumerable.Range(1, ContractsClient.MaxDefinitionsPerRequest + 1).Select(i => new ConId(i));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetDefinitionsAsync(tooMany, TestContext.Current.CancellationToken));

        Assert.Contains("at most 200", ex.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Stub.Requests);
    }

    [Fact]
    public async Task Reads_the_documented_stocks_payload_keyed_by_symbol()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-stock-by-symbol.json");
        var client = new ContractsClient(harness.ApiClient);

        var stocks = await client.GetStocksAsync(["IBM"], TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/trsrv/stocks", harness.LastRequest.Path);
        Assert.Equal("?symbols=IBM", harness.LastRequest.Query);
        Assert.Equal(Ibm, stocks["IBM"][0].Contracts[0].ConId);
        Assert.True(stocks["IBM"][0].Contracts[0].IsUnitedStates);
    }

    [Fact]
    public async Task Reads_the_documented_futures_payload_with_its_numeric_dates()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-future-by-symbol.json");
        var client = new ContractsClient(harness.ApiClient);

        var futures = await client.GetFuturesAsync(["ES"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/trsrv/futures", harness.LastRequest.Path);

        // These dates arrive as JSON numbers rather than the usual quoted yyyyMMdd.
        Assert.Equal(new LocalDate(2024, 12, 20), futures["ES"][0].ExpirationDate);
        Assert.Equal(new LocalDate(2024, 12, 19), futures["ES"][0].LastTradingDay);
    }

    [Fact]
    public async Task Reads_the_documented_exchange_listings_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-conids-by-exchange.json");
        var client = new ContractsClient(harness.ApiClient);

        var listings = await client.GetListingsByExchangeAsync("NYSE", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/trsrv/all-conids", harness.LastRequest.Path);
        Assert.Equal("BMO", listings[0].Ticker);
        Assert.Equal(new ConId(5094), listings[0].ConId);
    }

    [Fact]
    public async Task Reads_the_documented_currency_pairs_and_exchange_rate_payloads()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-currency-pairs.json");
        harness.RespondWithFixture("trading-contracts/get-exchange-rates.json");
        var client = new ContractsClient(harness.ApiClient);

        var pairs = await client.GetCurrencyPairsAsync("USD", TestContext.Current.CancellationToken);
        var rate = await client.GetExchangeRateAsync("EUR", "USD", TestContext.Current.CancellationToken);

        Assert.Equal("USD.SGD", pairs["USD"][0].Symbol);
        Assert.Equal(0.91324199m, rate.Rate);
        Assert.Contains("target=EUR", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("source=USD", harness.LastRequest.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_documented_bond_filters_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-bond-filters.json");
        var client = new ContractsClient(harness.ApiClient);

        var filters = await client.GetBondFiltersAsync("IBM", "e1400715", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/secdef/bond-filters", harness.LastRequest.Path);
        Assert.Equal("Maturity Date", filters.Filters[0].DisplayText);
        Assert.Equal("Jan 2025", filters.Filters[0].Options[0].Text);
    }

    [Fact]
    public async Task Reads_the_documented_trading_schedule_with_its_zone_and_wall_clock_times()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-trading-schedule.json");
        var client = new ContractsClient(harness.ApiClient);

        var schedules = await client.GetTradingScheduleAsync("STK", "IBM", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/trsrv/secdef/schedule", harness.LastRequest.Path);
        Assert.Equal("NYSE", schedules[0].Exchange);
        Assert.Equal(DateTimeZoneProviders.Tzdb["America/New_York"], schedules[0].TimeZone);

        var entry = schedules[0].Schedules[0];
        Assert.Equal(new LocalTime(0, 35), entry.TradingTimes[0].OpeningTime);
        Assert.Equal(new LocalTime(23, 30), entry.TradingTimes[0].ClosingTime);
        Assert.True(entry.TradingTimes[0].CancelsDayOrders);
    }

    [Fact]
    public async Task Reads_ibkrs_marker_dates_as_recurring_weekdays_rather_than_january_2000()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-contracts/get-trading-schedule.json");
        var client = new ContractsClient(harness.ApiClient);

        var schedules = await client.GetTradingScheduleAsync("STK", "IBM", cancellationToken: TestContext.Current.CancellationToken);

        // 20000101 means "any Saturday", not the first of January 2000.
        var first = schedules[0].Schedules[0].Date!.Value;
        Assert.True(first.IsRecurring);
        Assert.Equal(IsoDayOfWeek.Saturday, first.RecurringDay);
        Assert.Null(first.Date);

        var second = schedules[0].Schedules[1].Date!.Value;
        Assert.Equal(IsoDayOfWeek.Sunday, second.RecurringDay);
    }
}

public class TradingScheduleDateTests
{
    [Theory]
    [InlineData(1, IsoDayOfWeek.Saturday)]
    [InlineData(2, IsoDayOfWeek.Sunday)]
    [InlineData(3, IsoDayOfWeek.Monday)]
    [InlineData(4, IsoDayOfWeek.Tuesday)]
    [InlineData(5, IsoDayOfWeek.Wednesday)]
    [InlineData(6, IsoDayOfWeek.Thursday)]
    [InlineData(7, IsoDayOfWeek.Friday)]
    public void Maps_each_marker_date_to_the_weekday_ibkr_documents(int day, IsoDayOfWeek expected)
    {
        var value = TradingScheduleDate.ForDate(new LocalDate(2000, 1, day));

        Assert.True(value.IsRecurring);
        Assert.Equal(expected, value.RecurringDay);
    }

    [Fact]
    public void Treats_any_other_date_as_itself()
    {
        var value = TradingScheduleDate.ForDate(new LocalDate(2024, 12, 25));

        Assert.False(value.IsRecurring);
        Assert.Equal(new LocalDate(2024, 12, 25), value.Date);
    }

    [Fact]
    public void Round_trips_a_recurring_weekday_back_to_its_marker_date()
    {
        var value = TradingScheduleDate.ForWeekday(IsoDayOfWeek.Wednesday);

        Assert.Equal(new LocalDate(2000, 1, 5), value.ToWireDate());
    }
}
