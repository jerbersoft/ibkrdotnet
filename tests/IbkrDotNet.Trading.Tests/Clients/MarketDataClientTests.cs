using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class MarketDataClientTests
{
    private static readonly ConId Aapl = new(265598);

    [Fact]
    public async Task Sends_conids_and_fields_comma_separated()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("[]");
        var client = new MarketDataClient(harness.ApiClient);

        await client.GetSnapshotAsync(
            [Aapl, new ConId(8314)],
            [MarketDataField.LastPrice, MarketDataField.BidPrice],
            TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/marketdata/snapshot", harness.LastRequest.Path);
        Assert.Equal("?conids=265598%2C8314&fields=31%2C84", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Reads_a_snapshot_keyed_by_tick_identifier()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""
            [{"conid":265598,"31":"192.26","84":"192.25","86":"192.27","88":"300","85":"200",
              "87":"52.1M","55":"AAPL","6509":"RB","_updated":1702319517183,"server_id":"q0"}]
            """);
        var client = new MarketDataClient(harness.ApiClient);

        var snapshots = await client.GetSnapshotAsync([Aapl], cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = snapshots[0];
        Assert.Equal(Aapl, snapshot.ConId);
        Assert.Equal(192.26m, snapshot.LastPrice);
        Assert.Equal(192.25m, snapshot.BidPrice);
        Assert.Equal(192.27m, snapshot.AskPrice);
        Assert.Equal("AAPL", snapshot.Symbol);
        Assert.Equal("RB", snapshot.MarketDataAvailability);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702319517183), snapshot.UpdatedAt);
    }

    [Fact]
    public async Task Strips_the_marker_letter_ibkr_prefixes_to_a_price()
    {
        using var harness = new ClientHarness();

        // A leading letter marks the nature of the price: 'C' for a previous close, 'H' for a halt.
        harness.RespondWithJson("""[{"conid":265598,"31":"C212.11"}]""");
        var client = new MarketDataClient(harness.ApiClient);

        var snapshots = await client.GetSnapshotAsync([Aapl], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(212.11m, snapshots[0].LastPrice);

        // The raw value stays available for callers that need the marker.
        Assert.Equal("C212.11", snapshots[0].GetString(MarketDataField.LastPrice));
    }

    [Fact]
    public async Task Returns_null_for_a_field_ibkr_did_not_send()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""[{"conid":265598}]""");
        var client = new MarketDataClient(harness.ApiClient);

        var snapshots = await client.GetSnapshotAsync([Aapl], cancellationToken: TestContext.Current.CancellationToken);

        // The first request for an instrument is a pre-flight and returns no data.
        Assert.Null(snapshots[0].LastPrice);
        Assert.Null(snapshots[0].GetString(MarketDataField.Volume));
    }

    [Theory]
    [InlineData(101, 0, "at most 100 instruments")]
    [InlineData(1, 51, "at most 50 fields")]
    public async Task Refuses_a_request_that_exceeds_ibkrs_snapshot_limits(int conIds, int fields, string expected)
    {
        using var harness = new ClientHarness();
        var client = new MarketDataClient(harness.ApiClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetSnapshotAsync(
                [.. Enumerable.Range(1, conIds).Select(i => new ConId(i))],
                fields == 0 ? null : [.. Enumerable.Range(1, fields).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                TestContext.Current.CancellationToken));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Stub.Requests);
    }

    [Fact]
    public async Task Reads_the_documented_history_payload_with_its_mixed_time_units()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-market-data/get-md-history.json");
        var client = new MarketDataClient(harness.ApiClient);

        var history = await client.GetHistoryAsync(
            Aapl,
            HistoryPeriod.OneWeek,
            BarSize.OneDay,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/marketdata/history", harness.LastRequest.Path);
        Assert.Contains("period=1w", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("bar=1d", harness.LastRequest.Query, StringComparison.Ordinal);

        // barLength is seconds, mktDataDelay is milliseconds, and a bar's 't' is epoch milliseconds.
        Assert.Equal(Duration.FromDays(1), history.BarLength);
        Assert.Equal(Duration.Zero, history.MarketDataDelay);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1747229400000), history.Bars[0].Start);
        Assert.Equal(212.09m, history.Bars[0].Open);
        Assert.Equal(212.11m, history.Bars[0].Close);

        // chartPanStartTime is YYYYMMDD-hh:mm:ss.
        Assert.Equal(Instant.FromUtc(2025, 5, 21, 0, 0, 0), history.ChartPanStartTime);
    }

    [Fact]
    public async Task Formats_the_start_time_the_way_ibkr_expects()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-market-data/get-md-history.json");
        var client = new MarketDataClient(harness.ApiClient);

        await client.GetHistoryAsync(
            Aapl,
            HistoryPeriod.OneDay,
            BarSize.FiveMinutes,
            startTime: Instant.FromUtc(2024, 3, 15, 13, 30, 0),
            direction: HistoricalDataDirection.StartingAtStartTime,
            source: HistoricalDataSource.BidAsk,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("startTime=20240315-13%3A30%3A00", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("direction=1", harness.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("source=Bid_Ask", harness.LastRequest.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_forward_direction_without_a_start_time()
    {
        using var harness = new ClientHarness();
        var client = new MarketDataClient(harness.ApiClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetHistoryAsync(
                Aapl,
                HistoryPeriod.OneDay,
                BarSize.OneMinute,
                direction: HistoricalDataDirection.StartingAtStartTime,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("requires a start time", ex.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Stub.Requests);
    }

    // The fixture below ends '.live.json'. IBKR's published example carries a volumeFactor of 100 and
    // a volume of zero on every bar, so it cannot show what the factor does; this is a recorded
    // answer whose factor is 40 and whose bars traded.
    [Fact]
    public async Task Multiplies_a_bars_volume_by_the_responses_volume_factor()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-market-data/get-md-history.live.json");
        var client = new MarketDataClient(harness.ApiClient);

        var history = await client.GetHistoryAsync(
            Aapl,
            HistoryPeriod.OneMonth,
            BarSize.OneDay,
            cancellationToken: TestContext.Current.CancellationToken);

        // 'v' is the share count divided by volumeFactor, so the wire's number is not a volume. A
        // snapshot taken at the same moment put AAPL's day at 5,660,173 shares: the last bar's
        // 142,573.625 is nothing like it, and 142,573.625 x 40 is, to within the bar lagging the
        // tape and counting regular hours only.
        Assert.Equal(40m, history.VolumeFactor);
        Assert.Equal(700773.675m, history.Bars[0].RawVolume);
        Assert.Equal(28_030_947m, history.Bars[0].Volume);
        Assert.Equal(142573.625m, history.Bars[^1].RawVolume);
        Assert.Equal(5_702_945m, history.Bars[^1].Volume);

        // priceFactor scales the envelope's 'high' and 'low' summary strings -- 33622/847829.15/36000
        // is a high of 336.22 -- and never a bar, whose prices arrive as prices.
        Assert.Equal(100m, history.PriceFactor);
        Assert.Equal(307.58m, history.Bars[0].Open);
        Assert.Equal(329.43m, history.Bars[^1].Close);
        Assert.Equal(336.22m, history.Bars.Max(bar => bar.High));
    }

    [Fact]
    public async Task Reads_a_volume_as_sent_when_the_response_carries_no_factor()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""
            {"symbol":"AAPL","data":[{"o":307.58,"c":310.03,"h":311.49,"l":305.74,
              "v":700773.675,"t":1787059800000}]}
            """);
        var client = new MarketDataClient(harness.ApiClient);

        var history = await client.GetHistoryAsync(
            Aapl,
            HistoryPeriod.OneWeek,
            BarSize.OneDay,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(history.VolumeFactor);
        Assert.Equal(700773.675m, history.Bars[0].RawVolume);
        Assert.Equal(700773.675m, history.Bars[0].Volume);
    }

    // IBKR sends volumeFactor after the bars in its own example and before them in the recorded
    // answer, and a factor read second is still the factor for bars read first.
    [Theory]
    [InlineData("""{"volumeFactor":40,"data":[{"v":142573.625,"t":1789479000000}]}""")]
    [InlineData("""{"data":[{"v":142573.625,"t":1789479000000}],"volumeFactor":40}""")]
    public async Task Applies_the_volume_factor_whichever_side_of_the_bars_it_arrives(string json)
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(json);
        var client = new MarketDataClient(harness.ApiClient);

        var history = await client.GetHistoryAsync(
            Aapl,
            HistoryPeriod.OneDay,
            BarSize.OneDay,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(5_702_945m, history.Bars[0].Volume);
    }

    [Fact]
    public async Task Closes_one_stream_and_then_all_of_them()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-market-data/close-md-stream.json");
        harness.RespondWithFixture("trading-market-data/close-all-md-streams.json");
        var client = new MarketDataClient(harness.ApiClient);

        var one = await client.UnsubscribeAsync(Aapl, TestContext.Current.CancellationToken);
        var all = await client.UnsubscribeAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/marketdata/unsubscribe", harness.Stub.Requests[0].Path);
        Assert.Equal("""{"conid":265598}""", harness.Stub.Requests[0].Body);
        Assert.True(one.Success);

        Assert.Equal("/v1/api/iserver/marketdata/unsubscribeall", harness.Stub.Requests[1].Path);
        Assert.True(all.Unsubscribed);
    }
}
