using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Session;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class SessionClientTests
{
    [Fact]
    public async Task Reads_the_documented_brokerage_status_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-brokerage-status.success.json");
        var client = new SessionClient(harness.ApiClient);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/auth/status", harness.LastRequest.Path);
        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.True(status.Connected);
        Assert.True(status.Authenticated);
        Assert.True(status.Established);
        Assert.False(status.Competing);
        Assert.True(status.IsReadyToTrade);
        Assert.Equal("JifN19053", status.ServerInfo?.ServerName);
    }

    [Fact]
    public async Task Reads_the_documented_tickle_payload_including_the_session_token()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-session-token.success.json");
        var client = new SessionClient(harness.ApiClient);

        var tickle = await client.TickleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/tickle", harness.LastRequest.Path);
        Assert.Equal("bb665d0f55b6289d70bc7380089fc96f", tickle.Session);

        // 'ssoExpires' is milliseconds, not seconds.
        Assert.Equal(Duration.FromMilliseconds(460311), tickle.SsoExpires);
        Assert.True(tickle.AuthenticationStatus?.Established);
    }

    [Fact]
    public async Task Reads_a_tickle_that_ibkr_accepted_but_did_not_process()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-session-token.fail.json");
        var client = new SessionClient(harness.ApiClient);

        var tickle = await client.TickleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("failed to process request", tickle.Error);
        Assert.Null(tickle.Session);
    }

    [Fact]
    public async Task Sends_compete_and_publish_when_initializing()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/initialize-session.success.json");
        var client = new SessionClient(harness.ApiClient);

        var status = await client.InitializeAsync(compete: true, publish: true, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/auth/ssodh/init", harness.LastRequest.Path);
        Assert.Equal("""{"compete":true,"publish":true}""", harness.LastRequest.Body);
        Assert.True(status.IsReadyToTrade);
    }

    [Fact]
    public async Task Reads_the_documented_sso_validation_payload_with_nodatime_values()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-session-validation.success.json");
        var client = new SessionClient(harness.ApiClient);

        var validation = await client.ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/sso/validate", harness.LastRequest.Path);
        Assert.True(validation.Result);
        Assert.Equal("user1234", validation.UserName);

        // AUTH_TIME is epoch milliseconds. EXPIRES is milliseconds remaining in this documented
        // payload, but a live gateway sends an epoch timestamp for the same field.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702580846836), validation.AuthenticatedAt);
        Assert.Equal(Duration.FromMilliseconds(415890), validation.Expires?.Remaining);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702581069652), validation.LastAccessed);
    }

    [Fact]
    public async Task Reads_the_documented_logout_payload()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/logout.json");
        var client = new SessionClient(harness.ApiClient);

        var result = await client.LogoutAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/logout", harness.LastRequest.Path);
        Assert.True(result.Status);
    }
}

public class IbkrSessionManagerTests
{
    private static readonly Instant Now = Instant.FromUtc(2024, 5, 1, 12, 0, 0);

    private static IbkrSessionManager Create(ClientHarness harness, IbkrSessionState state) =>
        new(
            new SessionClient(harness.ApiClient),
            state,
            new FakeClock(Now),
            NullLogger<IbkrSessionManager>.Instance);

    [Fact]
    public async Task Records_the_session_token_so_it_can_be_sent_back_as_a_cookie()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-session-token.success.json");
        var state = new IbkrSessionState();
        using var manager = Create(harness, state);

        await manager.KeepAliveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("bb665d0f55b6289d70bc7380089fc96f", state.SessionToken);
        Assert.Equal(Now, state.ObtainedAt);
    }

    [Fact]
    public async Task Skips_initialization_when_the_tickle_already_reports_an_established_session()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/get-session-token.success.json");
        using var manager = Create(harness, new IbkrSessionState());

        var status = await manager.EnsureBrokerageSessionAsync(TestContext.Current.CancellationToken);

        Assert.True(status.IsReadyToTrade);
        Assert.Single(harness.Stub.Requests);
        Assert.Equal("/v1/api/tickle", harness.Stub.Requests[0].Path);
    }

    [Fact]
    public async Task Initializes_the_brokerage_session_when_the_tickle_shows_it_is_not_established()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("""{"session":"tok","iserver":{"authStatus":{"connected":true,"authenticated":false,"established":false,"competing":false}}}""");
        harness.RespondWithFixture("trading-session/initialize-session.success.json");
        using var manager = Create(harness, new IbkrSessionState());

        var status = await manager.EnsureBrokerageSessionAsync(TestContext.Current.CancellationToken);

        Assert.True(status.IsReadyToTrade);
        Assert.Equal(2, harness.Stub.Requests.Count);

        // The tickle runs first: it yields the token an OAuth caller must present as a cookie on
        // the /iserver call that follows.
        Assert.Equal("/v1/api/tickle", harness.Stub.Requests[0].Path);
        Assert.Equal("/v1/api/iserver/auth/ssodh/init", harness.Stub.Requests[1].Path);
    }

    [Fact]
    public async Task Discards_the_session_token_on_logout()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-session/logout.json");
        var state = new IbkrSessionState();
        state.Set("tok", Now);
        using var manager = Create(harness, state);

        await manager.LogoutAsync(TestContext.Current.CancellationToken);

        Assert.Null(state.SessionToken);
    }

    [Fact]
    public async Task Discards_the_session_token_even_when_logout_fails()
    {
        using var harness = new ClientHarness();
        harness.Stub.RespondWith(System.Net.HttpStatusCode.InternalServerError, "boom");
        var state = new IbkrSessionState();
        state.Set("tok", Now);
        using var manager = Create(harness, state);

        await Assert.ThrowsAsync<IbkrDotNet.Trading.Http.IbkrApiException>(
            () => manager.LogoutAsync(TestContext.Current.CancellationToken));

        // IBKR requires client-side cookies be discarded once a session ends, however it ended.
        Assert.Null(state.SessionToken);
    }
}
