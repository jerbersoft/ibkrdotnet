using System.Net;
using System.Text;
using System.Text.Json;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

/// <summary>
/// What the client does with a <c>429</c> after it has reported it. The limits table is inference in
/// places -- <c>/pa/allperiods</c> is in no IBKR limits table at all -- so a rejection is the only
/// evidence that the local pacing is wrong about an endpoint, and pacing the next request by the
/// same wrong figure is how one rejection becomes the pattern that gets an address blocked.
/// </summary>
public class RetryAfterFeedbackTests
{
    private static readonly IbkrRequest Probe = IbkrRequest.Get("/v1/api/probe/thing");
    private static readonly IbkrRequest OtherProbe = IbkrRequest.Get("/v1/api/probe/other");

    /// <summary>
    /// Rejects the first request with <c>429</c>, then answers everything after it.
    /// </summary>
    private static StubHttpMessageHandler RejectsOnce(string? retryAfter)
    {
        var stub = new StubHttpMessageHandler();
        stub.RespondWith(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error":"slow down"}""", Encoding.UTF8, "application/json"),
            };

            if (retryAfter is not null)
            {
                response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            }

            return response;
        });

        stub.AlwaysRespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        return stub;
    }

    private static (IbkrApiClient Client, FakeDelayScheduler Scheduler, StubHttpMessageHandler Stub, IbkrRateLimiterRegistry Registry)
        Build(string? retryAfter = "10", Action<IbkrTradingOptions>? configure = null)
    {
        var options = new IbkrTradingOptions();
        configure?.Invoke(options);

        var scheduler = new FakeDelayScheduler();
        var registry = new IbkrRateLimiterRegistry(options, scheduler);
        var stub = RejectsOnce(retryAfter);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost:5000") };

        return (new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance), scheduler, stub, registry);
    }

    private static Task<IbkrRateLimitExceededException> Rejected(IbkrApiClient client, IbkrRequest request) =>
        Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => client.SendAsync<JsonElement>(request, TestContext.Current.CancellationToken));

    [Fact]
    public async Task Holds_the_endpoint_for_the_window_ibkr_named()
    {
        var (client, scheduler, stub, registry) = Build(retryAfter: "10");
        using var _ = registry;

        await Rejected(client, Probe);
        await client.SendAsync<JsonElement>(Probe, TestContext.Current.CancellationToken);

        // No published limit covers /probe/thing, so without the feedback the second request would
        // have gone straight back out and been rejected again.
        Assert.Equal(Duration.FromSeconds(10), scheduler.TotalDelay);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task Reports_the_hold_it_applied_on_the_exception()
    {
        var (client, _, _, registry) = Build(retryAfter: "10");
        using var _r = registry;

        var ex = await Rejected(client, Probe);

        Assert.Equal(Duration.FromSeconds(10), ex.RetryAfter);
        Assert.Contains("held for", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_retry_after_given_as_an_absolute_date()
    {
        // The header permits either form, and IBKR documents sending neither, so both are read.
        var scheduler = new FakeDelayScheduler();
        var date = scheduler.Now.Plus(Duration.FromSeconds(20)).ToDateTimeOffset();

        using var registry = new IbkrRateLimiterRegistry(new IbkrTradingOptions(), scheduler);
        var stub = RejectsOnce(date.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        using var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        var ex = await Rejected(client, Probe);

        Assert.Equal(Duration.FromSeconds(20), ex.RetryAfter);
    }

    [Fact]
    public async Task Falls_back_to_the_matched_limits_own_window_when_no_header_is_sent()
    {
        var (client, scheduler, _, registry) = Build(retryAfter: null, configure: options =>
            options.RateLimiting.AdditionalLimits.Add(
                new IbkrRateLimit("/v1/api/probe/thing", "GET", 1, Duration.FromSeconds(5))));
        using var _r = registry;

        await Rejected(client, Probe);
        await client.SendAsync<JsonElement>(Probe, TestContext.Current.CancellationToken);

        // The window is the figure the client already believed; the rejection is only evidence that
        // it was not applied soon enough, so it is served again rather than replaced by a guess.
        Assert.Equal(Duration.FromSeconds(5), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Falls_back_to_the_configured_default_when_nothing_else_is_known()
    {
        var (client, scheduler, _, registry) = Build(retryAfter: null, configure: options =>
            options.RateLimiting.DefaultRetryAfter = Duration.FromSeconds(8));
        using var _r = registry;

        await Rejected(client, Probe);
        await client.SendAsync<JsonElement>(Probe, TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromSeconds(8), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Refuses_a_hold_longer_than_the_maximum_wait_rather_than_taking_it()
    {
        var (client, scheduler, stub, registry) = Build(retryAfter: "600");
        using var _r = registry;

        await Rejected(client, Probe);
        var ex = await Rejected(client, Probe);

        // Told, not silently blocked for ten minutes -- and nothing was sent to find out.
        Assert.Equal(Duration.FromSeconds(600), ex.RetryAfter);
        Assert.Contains("GET /v1/api/probe/thing", ex.Limit!, StringComparison.Ordinal);
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task Lets_the_endpoint_go_again_once_the_hold_elapses()
    {
        var (client, scheduler, _, registry) = Build(retryAfter: "10");
        using var _r = registry;

        await Rejected(client, Probe);
        scheduler.Advance(Duration.FromSeconds(10));
        await client.SendAsync<JsonElement>(Probe, TestContext.Current.CancellationToken);

        // A 429 can come from a second process on the same username or an address shared with
        // someone else, so the hold expires instead of permanently slowing a healthy client.
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Holds_only_the_endpoint_that_was_rejected()
    {
        var (client, scheduler, _, registry) = Build(retryAfter: "10");
        using var _r = registry;

        await Rejected(client, Probe);
        await client.SendAsync<JsonElement>(OtherProbe, TestContext.Current.CancellationToken);

        // One endpoint's limit must not become an outage for every other call in the process.
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Holds_everything_sharing_a_limit_with_the_rejected_request()
    {
        // The FYI routes are templated, so a rejection on one notification identifier is a rejection
        // of the limit, not of that identifier.
        var scheduler = new FakeDelayScheduler();
        using var registry = new IbkrRateLimiterRegistry(new IbkrTradingOptions(), scheduler);
        var stub = RejectsOnce("10");
        using var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        await Rejected(client, IbkrRequest.Put("/v1/api/fyi/notifications/abc123"));
        await client.SendAsync<JsonElement>(
            IbkrRequest.Put("/v1/api/fyi/notifications/def456"),
            TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromSeconds(10), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Does_not_hold_anything_when_pacing_is_disabled()
    {
        var (client, scheduler, stub, registry) = Build(retryAfter: "10", configure: options =>
            options.RateLimiting.Enabled = false);
        using var _r = registry;

        var ex = await Rejected(client, Probe);
        await client.SendAsync<JsonElement>(Probe, TestContext.Current.CancellationToken);

        // Turning pacing off turns off the feedback with it, but the caller is still told what IBKR
        // asked for, so they can honour it themselves -- and is not told anything is being held on
        // their behalf, because nothing is.
        Assert.Equal(Duration.FromSeconds(10), ex.RetryAfter);
        Assert.DoesNotContain("held for", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task Reports_the_rejection_from_the_handler_pipeline_too()
    {
        // The handler is the second-choice pacing path, but a caller using it should not silently
        // lose the feedback.
        var scheduler = new FakeDelayScheduler();
        using var registry = new IbkrRateLimiterRegistry(new IbkrTradingOptions(), scheduler);
        var stub = RejectsOnce("10");
        using var http = new HttpClient(new IbkrRateLimitHandler(registry) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://localhost:5000"),
        };

        var uri = new Uri("/v1/api/probe/thing", UriKind.Relative);
        (await http.GetAsync(uri, TestContext.Current.CancellationToken)).Dispose();
        (await http.GetAsync(uri, TestContext.Current.CancellationToken)).Dispose();

        Assert.Equal(Duration.FromSeconds(10), scheduler.TotalDelay);
    }
}
