using System.Text.Json;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class PortfolioAnalystClientTests
{
    private const string Fixtures = "trading-portfolio-analyst/";

    private static readonly AccountId Account = new("DU1234567");

    private static readonly AccountId[] Accounts = [Account];

    [Fact]
    public async Task Reads_the_documented_performance()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-performance.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetPerformanceAsync(
            Accounts, PerformancePeriod.OneMonth, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/pa/performance", harness.LastRequest.Path);
        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("getPerformanceData", performance.Id);
        Assert.Equal("TWR", performance.PortfolioMeasure);
        Assert.Equal("base", performance.CurrencyType);
        Assert.Equal(new AccountId("U1234567"), Assert.Single(performance.Included));

        var nav = Assert.IsType<PerformanceSeries>(performance.NetAssetValue);
        var entry = Assert.Single(nav.Data);
        Assert.Equal("acctid", entry.IdType);
        Assert.Equal(new LocalDate(2023, 1, 2), entry.Start);
        Assert.Equal(new LocalDate(2023, 12, 13), entry.End);

        // IBKR's published example quotes every figure; a live gateway sends them as JSON numbers.
        Assert.Equal([202767332.1223m, 215718598.8239m], entry.Values);
        Assert.Equal(new LocalDate(2022, 12, 30), entry.StartValue?.Date);
    }

    [Fact]
    public async Task Sends_the_period_as_the_text_ibkr_expects()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-performance.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await client.GetPerformanceAsync(
            Accounts, PerformancePeriod.MonthToDate, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.LastRequest.Body!);
        Assert.Equal("MTD", body.RootElement.GetProperty("period").GetString());
        Assert.Equal(
            "DU1234567",
            Assert.Single(body.RootElement.GetProperty("acctIds").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Reads_a_monthly_series_whose_dates_are_months_rather_than_days()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-performance.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetPerformanceAsync(
            Accounts, PerformancePeriod.OneYear, TestContext.Current.CancellationToken);

        // The reason PerformanceSeries.Dates is text: on one response the daily series is labelled
        // "20230102" and the monthly one "202301", and only the sibling 'freq' says which to expect.
        var daily = Assert.IsType<PerformanceSeries>(performance.CumulativePerformance);
        Assert.Equal("D", daily.Frequency);
        Assert.Equal("20230102", daily.Dates[0]);

        var monthly = Assert.IsType<PerformanceSeries>(performance.TimePeriodPerformance);
        Assert.Equal("M", monthly.Frequency);
        Assert.Equal("202301", monthly.Dates[0]);
        Assert.Equal(12, monthly.Dates.Count);
    }

    [Fact]
    public async Task Reads_the_documented_all_periods_performance()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allperiods.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetAllPeriodsPerformanceAsync(
            Accounts, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/pa/allperiods", harness.LastRequest.Path);
        Assert.Equal("getPerformanceAllPeriods", performance.Id);
        Assert.Equal(368, performance.DayCount);
        Assert.Equal(new AccountId("DU123456"), Assert.Single(performance.View));

        // The figures arrive under a property named after the account, which IBKR's documented
        // response schema does not mention exists.
        var history = performance.Accounts[new AccountId("DU123456")];
        Assert.Equal("USD", history.BaseCurrency);
        Assert.Equal(new LocalDate(2023, 4, 26), history.Start);
        Assert.Equal(new LocalDate(2024, 4, 26), history.End);
        Assert.Equal(["1D", "7D", "MTD", "1M", "YTD", "1Y"], history.PeriodNames);
        Assert.Equal(Instant.FromUtc(2024, 4, 26, 16, 46, 42), history.LastSuccessfulUpdate);

        var oneDay = Assert.IsType<PerformancePeriodSeries>(history.GetPeriod(PerformancePeriod.OneDay));
        Assert.Equal("D", oneDay.Frequency);
        Assert.Equal([0.0072m], oneDay.CumulativeReturns);
        Assert.Equal([1390987.0689m], oneDay.NetAssetValues);
        Assert.Equal(new LocalDate(2024, 4, 25), oneDay.StartValue?.Date);
        Assert.Equal(1381022.2557m, oneDay.StartValue?.Value);
    }

    [Fact]
    public async Task Keeps_the_period_names_apart_from_the_fields_beside_them()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allperiods.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetAllPeriodsPerformanceAsync(
            Accounts, TestContext.Current.CancellationToken);

        // 'baseCurrency', 'start', 'end', 'periods' and 'lastSuccessfulUpdate' are siblings of the
        // period names in the same object, so the converter has to tell one kind of key from the
        // other rather than taking everything it finds.
        var history = performance.Accounts[new AccountId("DU123456")];
        Assert.Equal(6, history.Periods.Count);
        Assert.Equal(history.PeriodNames.Order(), history.Periods.Keys.Order());
    }

    [Fact]
    public async Task Looks_a_period_up_without_regard_to_case()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allperiods.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetAllPeriodsPerformanceAsync(
            Accounts, TestContext.Current.CancellationToken);

        var history = performance.Accounts[new AccountId("DU123456")];
        Assert.NotNull(history.GetPeriod("mtd"));
        Assert.Null(history.GetPeriod("3M"));
    }

    [Fact]
    public async Task Reads_the_documented_allocation()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allocation.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var allocation = await client.GetAllocationAsync(
            Accounts,
            PortfolioAllocationType.Sector,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/pa/allocation", harness.LastRequest.Path);
        Assert.Equal("getAllocation", allocation.Id);
        Assert.Equal("USD", allocation.Currency);
        Assert.False(allocation.IsRealTime);
        Assert.Equal(new LocalDate(2024, 6, 11), allocation.Date);

        var sector = Assert.IsType<AllocationBreakdown>(
            allocation.GetBreakdown(PortfolioAllocationType.Sector));
        Assert.Equal(111598.1m, sector.LongPositions?.Total?.NetAssetValue);
        Assert.Equal(2, sector.LongPositions?.Items.Count);

        var technology = sector.LongPositions!.Items[1];
        Assert.Equal("57", technology.Id);
        Assert.Equal("Technology", technology.Name);
        Assert.Equal(0.24488781m, technology.Weight);
        Assert.Equal("#008fd5", technology.Color);

        // Weights are proportions of their own side, so the short side sums to one as well.
        Assert.Equal(-42904.21m, sector.ShortPositions?.Total?.NetAssetValue);
        Assert.Equal(1m, sector.ShortPositions?.Total?.Weight);
    }

    [Fact]
    public async Task Sends_the_allocation_request_ibkr_documents()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allocation.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await client.GetAllocationAsync(
            Accounts,
            PortfolioAllocationType.FinancialInstrument,
            currency: "GBP",
            asOfDate: new LocalDate(2024, 6, 11),
            model: "Balanced",
            cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.LastRequest.Body!);
        var root = body.RootElement;
        Assert.Equal("FINANCIAL_INSTRUMENT", root.GetProperty("type").GetString());
        Assert.Equal("GBP", root.GetProperty("currency").GetString());
        Assert.Equal("20240611", root.GetProperty("date").GetString());
        Assert.Equal("Balanced", root.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Omits_the_allocation_options_that_were_not_given()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allocation.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await client.GetAllocationAsync(
            Accounts, PortfolioAllocationType.All, cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.LastRequest.Body!);
        var root = body.RootElement;
        Assert.Equal("ALL", root.GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("currency", out _));
        Assert.False(root.TryGetProperty("date", out _));
        Assert.False(root.TryGetProperty("model", out _));
    }

    [Fact]
    public async Task Sends_the_transaction_request_ibkr_documents()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-transactions.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await client.GetTransactionsAsync(
            Accounts,
            [new ConId(265598)],
            currency: "USD",
            days: 365,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/pa/transactions", harness.LastRequest.Path);

        using var body = JsonDocument.Parse(harness.LastRequest.Body!);
        var root = body.RootElement;
        Assert.Equal(265598, Assert.Single(root.GetProperty("conids").EnumerateArray()).GetInt64());
        Assert.Equal("USD", root.GetProperty("currency").GetString());
        Assert.Equal(365, root.GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task Sends_a_currency_the_caller_did_not_name()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-transactions.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await client.GetTransactionsAsync(
            Accounts, [new ConId(265598)], cancellationToken: TestContext.Current.CancellationToken);

        // IBKR documents 'currency' as optional with a default of USD, and then answers a request
        // without it "Bad Request: acctIds, currency and conids are required" -- which costs the
        // caller the endpoint's fifteen-minute window to find out. The documented default is sent
        // rather than omitted.
        using var body = JsonDocument.Parse(harness.LastRequest.Body!);
        Assert.Equal("USD", body.RootElement.GetProperty("currency").GetString());

        // 'days' really is optional, and stays omitted.
        Assert.False(body.RootElement.TryGetProperty("days", out _));
    }

    [Theory]
    [InlineData(PerformancePeriod.OneDay, "1D")]
    [InlineData(PerformancePeriod.SevenDays, "7D")]
    [InlineData(PerformancePeriod.MonthToDate, "MTD")]
    [InlineData(PerformancePeriod.OneMonth, "1M")]
    [InlineData(PerformancePeriod.YearToDate, "YTD")]
    [InlineData(PerformancePeriod.OneYear, "1Y")]
    public void Renders_every_period_as_ibkr_spells_it(PerformancePeriod period, string expected) =>
        Assert.Equal(expected, period.ToWireValue());

    [Theory]
    [InlineData(PortfolioAllocationType.FinancialInstrument, "FINANCIAL_INSTRUMENT")]
    [InlineData(PortfolioAllocationType.AssetClass, "ASSET_CLASS")]
    [InlineData(PortfolioAllocationType.Sector, "SECTOR")]
    [InlineData(PortfolioAllocationType.Region, "REGION")]
    [InlineData(PortfolioAllocationType.Country, "COUNTRY")]
    [InlineData(PortfolioAllocationType.All, "ALL")]
    public void Renders_every_allocation_type_as_ibkr_spells_it(
        PortfolioAllocationType type, string expected) =>
        Assert.Equal(expected, type.ToWireValue());

    [Fact]
    public async Task Refuses_a_request_with_no_account()
    {
        using var harness = new ClientHarness();
        var client = new PortfolioAnalystClient(harness.ApiClient);

        // Cheap to enforce and expensive to discover: a rejected request still spends the endpoint's
        // fifteen-minute window.
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAllPeriodsPerformanceAsync(
            [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refuses_a_transaction_request_with_no_contract()
    {
        using var harness = new ClientHarness();
        var client = new PortfolioAnalystClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetTransactionsAsync(
            Accounts, [], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Writes_the_account_keyed_shape_back_out_as_it_arrived()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allperiods.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetAllPeriodsPerformanceAsync(
            Accounts, TestContext.Current.CancellationToken);

        // The converter has to write the account and period keys back out, not just read them, and
        // nothing else in the library would notice if it did not.
        var written = JsonSerializer.Serialize(performance, IbkrJson.Options);
        var round = JsonSerializer.Deserialize<PerformanceAllPeriods>(written, IbkrJson.Options)!;

        Assert.Equal(performance.Accounts.Keys, round.Accounts.Keys);
        Assert.Equal(performance.PortfolioMeasure, round.PortfolioMeasure);
        Assert.Equal(performance.DayCount, round.DayCount);

        var before = performance.Accounts[Account];
        var after = round.Accounts[Account];
        Assert.Equal(before.PeriodNames, after.PeriodNames);
        Assert.Equal(before.Start, after.Start);
        Assert.Equal(before.End, after.End);
        Assert.Equal(before.LastSuccessfulUpdate, after.LastSuccessfulUpdate);
        Assert.Equal(before.Periods.Keys.Order(), after.Periods.Keys.Order());

        var year = after.GetPeriod(PerformancePeriod.OneYear)!;
        Assert.Equal(before.GetPeriod(PerformancePeriod.OneYear)!.NetAssetValues, year.NetAssetValues);
        Assert.Equal(before.GetPeriod(PerformancePeriod.OneYear)!.StartValue, year.StartValue);
    }

    [Fact]
    public void Rejects_a_null_api_client() =>
        Assert.Throws<ArgumentNullException>(() => new PortfolioAnalystClient(null!));

    // ---- Captured from a live gateway ------------------------------------------------------------
    //
    // The fixtures below end '.live.json'. Each one contradicts IBKR's reference somewhere: the
    // figures IBKR quotes as strings arrive as numbers, the transaction date IBKR documents is not
    // the one worth reading, the allocation date IBKR documents is absent, and 'nd' is not what IBKR
    // says it is anywhere. The account identifier is replaced with a placeholder; every other value
    // is verbatim.

    [Fact]
    public async Task Reads_a_live_performance_whose_figures_are_numbers_rather_than_strings()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-performance.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetPerformanceAsync(
            Accounts, PerformancePeriod.OneMonth, TestContext.Current.CancellationToken);

        var nav = Assert.IsType<PerformanceSeries>(performance.NetAssetValue);
        var entry = Assert.Single(nav.Data);
        Assert.Equal(81100.346542m, entry.Values[0]);
        Assert.Equal(81030.226542m, entry.StartValue?.Value);

        // 'nd' is 33 while the series holds 22 points, so it is not the count of data points IBKR's
        // reference calls it. It is near the calendar width of the window -- 32 days from start to
        // end inclusive -- but not equal to it, which is why DayCount is documented as approximate
        // rather than as a rule.
        Assert.Equal(33, performance.DayCount);
        Assert.Equal(22, nav.Dates.Count);
        Assert.Equal(
            32,
            Period.Between(entry.Start!.Value, entry.End!.Value, PeriodUnits.Days).Days + 1);
    }

    [Fact]
    public async Task Reads_a_live_all_periods_performance()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allperiods.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var performance = await client.GetAllPeriodsPerformanceAsync(
            Accounts, TestContext.Current.CancellationToken);

        var history = performance.Accounts[Account];
        Assert.Equal(["1D", "7D", "MTD", "1M", "YTD", "1Y"], history.PeriodNames);

        // The one field on the API in this format, and the reason it is read as UTC: the request was
        // fired at 17:33:08 UTC from a host an hour ahead of it.
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 17, 33, 8), history.LastSuccessfulUpdate);

        // Every period is sampled daily, including the year, so 1Y is 262 trading days rather than
        // twelve months.
        var year = Assert.IsType<PerformancePeriodSeries>(history.GetPeriod(PerformancePeriod.OneYear));
        Assert.Equal("D", year.Frequency);
        Assert.Equal(262, year.Dates.Count);
        Assert.Equal(year.Dates.Count, year.NetAssetValues.Count);
        Assert.Equal(year.Dates.Count, year.CumulativeReturns.Count);
    }

    [Fact]
    public async Task Reads_a_live_transaction_history()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-transactions.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var history = await client.GetTransactionsAsync(
            Accounts, [new ConId(265598)], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Instant.FromUtc(2025, 9, 8, 0, 0, 0), history.From);
        Assert.Equal(Instant.FromUtc(2026, 9, 8, 0, 0, 0), history.To);
        Assert.Equal(366, history.DayCount);

        var purchase = history.Transactions[0];
        Assert.Equal("Buy", purchase.Type);
        Assert.Equal(new ConId(265598), purchase.ConId);
        Assert.Equal(10m, purchase.Quantity);
        Assert.Equal(267.71m, purchase.Price);

        // Buying is a positive quantity and a negative amount: the cash went the other way.
        Assert.Equal(-2677.1m, purchase.Amount);

        // The undocumented 'rawDate' is the machine-readable one; the documented 'date' is Java's
        // rendering, whose zone is an abbreviation rather than an identifier.
        Assert.Equal(new LocalDate(2025, 11, 5), purchase.Date);
        Assert.Equal("Wed Nov 05 00:00:00 EST 2025", purchase.DateText);

        // Not only trades.
        Assert.Contains(history.Transactions, transaction => transaction.Type == "Payment In Lieu");
    }

    [Fact]
    public async Task Reads_a_live_realized_pnl_whose_side_flag_contradicts_its_documentation()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-transactions.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var history = await client.GetTransactionsAsync(
            Accounts, [new ConId(265598)], cancellationToken: TestContext.Current.CancellationToken);

        var realized = Assert.IsType<RealizedProfitAndLoss>(history.RealizedProfitAndLoss);
        Assert.Equal(113.61057664m, realized.Amount);

        var entry = realized.Data[0];
        Assert.Equal(new LocalDate(2026, 9, 8), entry.Date);

        // IBKR documents 'side' as L for LOSS and G for GAIN. This entry is L and its amount is
        // +92.71, and 'positionSide' -- which IBKR does not document at all -- says 'long'. That is
        // why Side is text and the sign of Amount is what tells a gain from a loss.
        Assert.Equal("L", entry.Side);
        Assert.Equal("long", entry.PositionSide);
        Assert.True(entry.Amount > 0m);

        // Quoted here, and a JSON number on the transactions in the very same response.
        Assert.Equal(new ConId(265598), entry.ConId);
    }

    [Fact]
    public async Task Reads_a_live_allocation_that_sends_no_date_and_no_short_side()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-allocation.live.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        var allocation = await client.GetAllocationAsync(
            Accounts,
            PortfolioAllocationType.All,
            cancellationToken: TestContext.Current.CancellationToken);

        // Asking for ALL returns the five real categories, and never a key named ALL.
        Assert.Equal(5, allocation.Allocations.Count);
        Assert.Null(allocation.GetBreakdown(PortfolioAllocationType.All));

        // IBKR documents 'date' without qualification and omits it when the figures are for now.
        Assert.True(allocation.IsRealTime);
        Assert.Null(allocation.Date);

        var assetClass = Assert.IsType<AllocationBreakdown>(
            allocation.GetBreakdown(PortfolioAllocationType.AssetClass));
        Assert.Null(assetClass.ShortPositions);
        Assert.Equal(79933.252596m, assetClass.LongPositions?.Total?.NetAssetValue);
        Assert.Equal(
            ["Equities", "Cash"],
            assetClass.LongPositions!.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Cannot_read_the_transaction_history_ibkr_publishes_as_an_example()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-transactions.documented.json");
        var client = new PortfolioAnalystClient(harness.ApiClient);

        // IBKR's published example puts the literal string "string" where a realized-pnl object
        // belongs -- a schema placeholder that escaped into the documentation -- so its own example
        // does not satisfy its own schema. Pinned rather than tolerated: a live gateway sends
        // objects, and quietly dropping array entries that do not parse would hide the loss.
        var exception = await Assert.ThrowsAsync<IbkrApiException>(() => client.GetTransactionsAsync(
            Accounts, [new ConId(265598)], cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsType<JsonException>(exception.InnerException);
        Assert.Contains("$.rpnl.data[0]", exception.InnerException.Message, StringComparison.Ordinal);
    }
}
