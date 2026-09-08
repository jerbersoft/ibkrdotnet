using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class WatchlistsClientTests
{
    [Fact]
    public async Task Unwraps_the_documented_listing_envelope()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/get-all-watchlists.success.json");
        var client = new WatchlistsClient(harness.ApiClient);

        var watchlists = await client.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/watchlists", harness.LastRequest.Path);
        Assert.Equal(string.Empty, harness.LastRequest.Query);

        var watchlist = Assert.Single(watchlists);
        Assert.Equal("1234", watchlist.Id);
        Assert.Equal("Test Watchlist", watchlist.Name);
        Assert.False(watchlist.IsReadOnly);

        // 'modified' is epoch milliseconds.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702581306241), watchlist.ModifiedAt);
    }

    [Fact]
    public async Task Sends_the_user_watchlist_filter_only_when_asked_for_it()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/get-all-watchlists.success.json");
        var client = new WatchlistsClient(harness.ApiClient);

        await client.GetAllAsync(userCreatedOnly: true, TestContext.Current.CancellationToken);

        Assert.Equal("?SC=USER_WATCHLIST", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Reads_a_single_watchlist_and_its_instruments()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/get-specific-watchlist.success.json");
        var client = new WatchlistsClient(harness.ApiClient);

        var watchlist = await client.GetAsync("1234", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/watchlist", harness.LastRequest.Path);
        Assert.Equal("?id=1234", harness.LastRequest.Query);
        Assert.Equal("Test Watchlist", watchlist.Name);
        Assert.False(watchlist.IsReadOnly);

        var instrument = Assert.Single(watchlist.Instruments);
        Assert.Equal(new ConId(8314), instrument.ConId);
        Assert.Equal("IBM", instrument.Ticker);
        Assert.Equal("INTL BUSINESS MACHINES CORP", instrument.Name);

        // IBKR sends the conid twice, as a number under 'conid' and as a string under 'C'.
        Assert.Equal("8314", instrument.ConIdText);
        Assert.Equal("STK", instrument.SecurityType);
        Assert.Equal("STK", instrument.AssetClass);
    }

    [Fact]
    public async Task Sends_conids_as_rows_under_ibkrs_shorthand_key()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/create-watchlist.success.json");
        var client = new WatchlistsClient(harness.ApiClient);

        var created = await client.CreateAsync(
            "1234",
            "Test Watchlist",
            [new ConId(8314), new ConId(265598)],
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/watchlist", harness.LastRequest.Path);
        Assert.Equal(
            """{"id":"1234","name":"Test Watchlist","rows":[{"C":"8314"},{"C":"265598"}]}""",
            harness.LastRequest.Body);
        Assert.Equal("1234", created.Id);
        Assert.Equal("1234987651621", created.Hash);
    }

    [Fact]
    public async Task Reads_the_creation_response_ibkr_actually_documents()
    {
        // IBKR types the creation response's 'instruments' as a list of anything and its published
        // example holds strings, not the objects the read endpoint returns. The client does not
        // declare the member, so whatever arrives there is skipped rather than parsed.
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/create-watchlist.documented.json");
        var client = new WatchlistsClient(harness.ApiClient);

        var created = await client.CreateAsync(
            "1234", "Test Watchlist", [new ConId(8314)], TestContext.Current.CancellationToken);

        Assert.True(created.IsReadOnly);
        Assert.Empty(created.Instruments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("my-list")]
    [InlineData("1234a")]
    public async Task Refuses_a_watchlist_id_ibkr_will_not_accept(string watchlistId)
    {
        using var harness = new ClientHarness();
        var client = new WatchlistsClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.CreateAsync(
                watchlistId, "Test Watchlist", [new ConId(8314)], TestContext.Current.CancellationToken));

        Assert.Empty(harness.Stub.Requests);
    }

    [Fact]
    public async Task Reports_the_watchlist_ibkr_confirms_it_deleted()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-watchlists/delete-watchlist.success.json");
        var client = new WatchlistsClient(harness.ApiClient);

        var deletion = await client.DeleteAsync("1234", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/watchlist", harness.LastRequest.Path);
        Assert.Equal("?id=1234", harness.LastRequest.Query);
        Assert.Equal("1234", deletion.DeletedId);
    }
}
