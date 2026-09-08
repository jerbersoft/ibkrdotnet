using System.Net;
using System.Text;
using System.Text.Json;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Extensions.DependencyInjection.Tests;

/// <summary>
/// Pacing is applied by <see cref="IbkrApiClient"/> rather than by a handler in the pipeline, so
/// nothing in the handler chain can show that a container-built client is paced at all. These build
/// the real pipeline over a stand-in server and watch the requests arrive.
/// </summary>
public class RateLimitingRegistrationTests
{
    private const string Path = "/v1/api/probe/thing";

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private static ServiceProvider Build(CountingHandler server, Action<IbkrTradingOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrTrading(configure);
        services
            .AddHttpClient(IbkrApiClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => server);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Paces_requests_made_through_the_container()
    {
        using var server = new CountingHandler();
        using var provider = Build(server, options =>
        {
            options.RateLimiting.MaxWait = Duration.Zero;
            options.RateLimiting.AdditionalLimits.Add(
                new IbkrRateLimit(Path, "GET", 1, Duration.FromMinutes(15)));
        });

        var client = provider.GetRequiredService<IIbkrApiClient>();
        var request = IbkrRequest.Get(Path);

        await client.SendAsync<JsonElement>(request, TestContext.Current.CancellationToken);

        // MaxWait is zero, so the second request cannot be paced -- it can only be refused. That it
        // is refused at all is the proof that the limiters reached the client.
        var ex = await Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => client.SendAsync<JsonElement>(request, TestContext.Current.CancellationToken));

        // Measured against a real clock, so it is a shade under the full window.
        Assert.NotNull(ex.RetryAfter);
        Assert.InRange(ex.RetryAfter.Value, Duration.FromMinutes(14), Duration.FromMinutes(15));
        Assert.Equal(1, server.Count);
    }

    [Fact]
    public async Task Does_not_charge_the_pacing_wait_to_the_request_timeout()
    {
        using var server = new CountingHandler();
        using var provider = Build(server, options =>
        {
            // A window more than twice the timeout: pacing inside the timeout could not survive it.
            options.Timeout = Duration.FromMilliseconds(250);
            options.RateLimiting.AdditionalLimits.Add(
                new IbkrRateLimit(Path, "GET", 1, Duration.FromMilliseconds(600)));
        });

        var client = provider.GetRequiredService<IIbkrApiClient>();
        var request = IbkrRequest.Get(Path);

        await client.SendAsync<JsonElement>(request, TestContext.Current.CancellationToken);
        await client.SendAsync<JsonElement>(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, server.Count);
    }

    [Fact]
    public void Shares_one_set_of_limiters_across_the_application()
    {
        using var server = new CountingHandler();
        using var provider = Build(server, _ => { });

        // The windows are the state. Two registries would mean two of every window, which against a
        // one-per-fifteen-minutes limit is exactly twice what IBKR permits.
        Assert.Same(
            provider.GetRequiredService<IbkrRateLimiterRegistry>(),
            provider.GetRequiredService<IbkrRateLimiterRegistry>());
    }
}
