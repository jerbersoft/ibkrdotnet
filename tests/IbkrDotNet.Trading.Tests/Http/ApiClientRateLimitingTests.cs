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
/// Pacing lives in <see cref="IbkrApiClient"/> rather than in a handler, so that the wait is not
/// charged to <see cref="HttpClient.Timeout"/>. These cover both halves of that: that requests are
/// still paced, and that the pacing costs the caller nothing.
/// </summary>
public class ApiClientRateLimitingTests
{
    private static readonly IbkrRequest Request = IbkrRequest.Get("/v1/api/probe/thing");

    private static StubHttpMessageHandler AlwaysOk()
    {
        var stub = new StubHttpMessageHandler();
        stub.AlwaysRespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        return stub;
    }

    private static IbkrTradingOptions OneRequestPer(Duration window)
    {
        var options = new IbkrTradingOptions();
        options.RateLimiting.AdditionalLimits.Add(
            new IbkrRateLimit("/v1/api/probe/thing", "GET", 1, window));

        return options;
    }

    [Fact]
    public async Task Paces_a_request_before_sending_it()
    {
        var scheduler = new FakeDelayScheduler();
        using var registry = new IbkrRateLimiterRegistry(OneRequestPer(Duration.FromSeconds(5)), scheduler);
        using var http = new HttpClient(AlwaysOk()) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);
        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromSeconds(5), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Paces_a_raw_request_too()
    {
        var scheduler = new FakeDelayScheduler();
        using var registry = new IbkrRateLimiterRegistry(OneRequestPer(Duration.FromSeconds(5)), scheduler);
        using var http = new HttpClient(AlwaysOk()) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        (await client.SendRawAsync(Request, TestContext.Current.CancellationToken)).Dispose();
        (await client.SendRawAsync(Request, TestContext.Current.CancellationToken)).Dispose();

        Assert.Equal(Duration.FromSeconds(5), scheduler.TotalDelay);
    }

    [Fact]
    public async Task Reports_a_wait_longer_than_the_maximum_rather_than_taking_it()
    {
        var options = OneRequestPer(Duration.FromMinutes(15));
        options.RateLimiting.MaxWait = Duration.FromSeconds(30);

        var scheduler = new FakeDelayScheduler();
        using var registry = new IbkrRateLimiterRegistry(options, scheduler);
        using var http = new HttpClient(AlwaysOk()) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<IbkrRateLimitExceededException>(() =>
            client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken));

        // Not an IbkrApiException about a timeout: nothing was sent, and the caller is told what to
        // wait for rather than being handed a network-shaped failure.
        Assert.Equal(Duration.FromMinutes(15), ex.RetryAfter);
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Does_not_spend_the_request_timeout_on_its_own_pacing()
    {
        // The regression this arrangement exists for. HttpClient.Timeout covers every handler in the
        // chain, so pacing inside one is charged to the caller: with the shipped defaults the
        // longest permitted wait and the whole request budget are both thirty seconds, and a paced
        // request failed as "timed out" without a byte having left the process. Here the window is
        // more than twice the timeout, so the old arrangement cannot pass.
        var window = Duration.FromMilliseconds(600);
        using var registry = new IbkrRateLimiterRegistry(
            OneRequestPer(window),
            new SystemDelayScheduler(SystemClock.Instance));

        using var http = new HttpClient(AlwaysOk())
        {
            BaseAddress = new Uri("https://localhost:5000"),
            Timeout = TimeSpan.FromMilliseconds(250),
        };

        var client = new IbkrApiClient(http, registry, NullLogger<IbkrApiClient>.Instance);

        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);
        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sends_without_pacing_when_no_limiters_are_supplied()
    {
        var stub = AlwaysOk();
        using var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost:5000") };
        var client = new IbkrApiClient(http, rateLimiters: null, NullLogger<IbkrApiClient>.Instance);

        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);
        await client.SendAsync<JsonElement>(Request, TestContext.Current.CancellationToken);

        Assert.Equal(2, stub.Requests.Count);
    }
}
