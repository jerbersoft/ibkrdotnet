using System.Net;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

public class RateLimitHandlerTests
{
    private static (HttpClient Client, FakeDelayScheduler Scheduler, StubHttpMessageHandler Stub) Build(
        Action<IbkrTradingOptions>? configure = null)
    {
        var options = new IbkrTradingOptions();
        configure?.Invoke(options);

        var scheduler = new FakeDelayScheduler();
        var stub = new StubHttpMessageHandler();
        var registry = new IbkrRateLimiterRegistry(options, scheduler);
        var handler = new IbkrRateLimitHandler(registry) { InnerHandler = stub };

        return (new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5000") }, scheduler, stub);
    }

    [Fact]
    public async Task Lets_the_first_request_through_without_delay()
    {
        var (client, scheduler, _) = Build();

        await client.GetAsync(new Uri("/v1/api/iserver/accounts", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Paces_the_global_ten_per_second_limit()
    {
        var (client, scheduler, _) = Build();
        var uri = new Uri("/v1/api/iserver/secdef/search", UriKind.Relative);

        // Ten requests fit inside one second; the eleventh must wait for the window to roll.
        for (var i = 0; i < 11; i++)
        {
            await client.GetAsync(uri, TestContext.Current.CancellationToken);
        }

        Assert.Single(scheduler.Delays);
        Assert.Equal(Duration.FromSeconds(1), scheduler.Delays[0]);
    }

    [Fact]
    public async Task Applies_the_documented_five_second_limit_on_trade_history()
    {
        var (client, scheduler, _) = Build();
        var uri = new Uri("/v1/api/iserver/account/trades", UriKind.Relative);

        await client.GetAsync(uri, TestContext.Current.CancellationToken);
        await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromSeconds(5), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Matches_a_templated_fyi_route()
    {
        var (client, scheduler, _) = Build();

        await client.PutAsync(
            new Uri("/v1/api/fyi/notifications/abc123", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);
        await client.PutAsync(
            new Uri("/v1/api/fyi/notifications/def456", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        // Both requests hit the same per-endpoint bucket despite different identifiers.
        Assert.Equal(Duration.FromSeconds(1), scheduler.Delays[0]);
    }

    [Fact]
    public async Task Refuses_rather_than_blocking_for_a_fifteen_minute_limit()
    {
        var (client, _, _) = Build();
        var uri = new Uri("/v1/api/iserver/scanner/params", UriKind.Relative);

        await client.GetAsync(uri, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => client.GetAsync(uri, TestContext.Current.CancellationToken));

        Assert.Equal(Duration.FromMinutes(15), ex.RetryAfter);
        Assert.Contains("scanner/params", ex.Limit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Honours_a_raised_maximum_wait()
    {
        var (client, scheduler, _) = Build(o => o.RateLimiting.MaxWait = Duration.FromMinutes(20));
        var uri = new Uri("/v1/api/iserver/scanner/params", UriKind.Relative);

        await client.GetAsync(uri, TestContext.Current.CancellationToken);
        await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromMinutes(15), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Sends_without_pacing_when_rate_limiting_is_disabled()
    {
        var (client, scheduler, stub) = Build(o => o.RateLimiting.Enabled = false);
        var uri = new Uri("/v1/api/iserver/account/trades", UriKind.Relative);

        await client.GetAsync(uri, TestContext.Current.CancellationToken);
        await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task Keeps_pacing_across_the_handler_rotation_that_ihttpclientfactory_performs()
    {
        // IHttpClientFactory builds a fresh handler chain on its own lifetime. The windows live in
        // the registry so a rotation does not hand the caller a clean slate -- which would be
        // invisible against a one-second limit and completely wrong against a fifteen-minute one.
        var scheduler = new FakeDelayScheduler();
        var registry = new IbkrRateLimiterRegistry(new IbkrTradingOptions(), scheduler);
        var uri = new Uri("/v1/api/iserver/scanner/params", UriKind.Relative);

        using (var firstStub = new StubHttpMessageHandler())
        using (var firstClient = new HttpClient(new IbkrRateLimitHandler(registry) { InnerHandler = firstStub })
               {
                   BaseAddress = new Uri("https://localhost:5000"),
               })
        {
            await firstClient.GetAsync(uri, TestContext.Current.CancellationToken);
        }

        using var secondStub = new StubHttpMessageHandler();
        using var secondClient = new HttpClient(new IbkrRateLimitHandler(registry) { InnerHandler = secondStub })
        {
            BaseAddress = new Uri("https://localhost:5000"),
        };

        await Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => secondClient.GetAsync(uri, TestContext.Current.CancellationToken));

        registry.Dispose();
    }

    [Fact]
    public async Task Passes_the_response_through_untouched()
    {
        var (client, _, stub) = Build();
        stub.RespondWith(HttpStatusCode.TooManyRequests, """{"error":"slow down"}""");

        var response = await client.GetAsync(
            new Uri("/v1/api/iserver/accounts", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // The handler records the rejection against the limiters, but does not act on it for the
        // caller: turning a 429 into an exception belongs to IbkrApiClient.
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Charges_its_wait_to_the_client_timeout_which_is_why_it_is_not_the_default()
    {
        // HttpClient.Timeout covers the whole handler chain, so a wait taken in here is spent out of
        // the caller's request budget. This is the behaviour the handler's remarks warn about and the
        // reason IbkrApiClient paces before the send instead; it is pinned so that the warning
        // cannot quietly stop being true.
        var options = new IbkrTradingOptions();
        options.RateLimiting.AdditionalLimits.Add(
            new IbkrRateLimit("/v1/api/probe/thing", "GET", 1, Duration.FromMilliseconds(600)));

        using var registry = new IbkrRateLimiterRegistry(
            options,
            new SystemDelayScheduler(SystemClock.Instance));

        using var stub = new StubHttpMessageHandler();
        stub.AlwaysRespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK));

        using var client = new HttpClient(new IbkrRateLimitHandler(registry) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://localhost:5000"),
            Timeout = TimeSpan.FromMilliseconds(250),
        };

        var uri = new Uri("/v1/api/probe/thing", UriKind.Relative);
        await client.GetAsync(uri, TestContext.Current.CancellationToken);

        // The second request never reaches the stub: the timeout fires while the handler is waiting.
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetAsync(uri, TestContext.Current.CancellationToken));

        Assert.Single(stub.Requests);
    }
}
