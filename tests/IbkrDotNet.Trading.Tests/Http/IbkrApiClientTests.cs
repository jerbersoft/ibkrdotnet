using System.Net;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

public class IbkrApiClientTests
{
    private sealed record Account
    {
        [JsonPropertyName("accountId")]
        public string? AccountId { get; init; }
    }

    private static (IbkrApiClient Client, StubHttpMessageHandler Stub) Build()
    {
        var stub = new StubHttpMessageHandler();
        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://localhost:5000") };
        return (new IbkrApiClient(httpClient, NullLogger<IbkrApiClient>.Instance), stub);
    }

    [Fact]
    public async Task Sends_the_method_path_and_query_it_was_given()
    {
        var (client, stub) = Build();
        stub.RespondWithJson("""{"accountId":"U1234567"}""");

        await client.SendAsync<Account>(
            IbkrRequest.Get("/v1/api/iserver/account/trades").WithQuery("days", 7L),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, stub.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/account/trades", stub.LastRequest.Path);
        Assert.Equal("?days=7", stub.LastRequest.Query);
    }

    [Fact]
    public async Task Serializes_a_json_body()
    {
        var (client, stub) = Build();
        stub.RespondWithJson("{}");

        await client.SendAsync(
            IbkrRequest.Post("/v1/api/iserver/auth/ssodh/init")
                .WithJsonBody(new { compete = true, publish = true }),
            TestContext.Current.CancellationToken);

        Assert.Equal("""{"compete":true,"publish":true}""", stub.LastRequest.Body);
    }

    [Fact]
    public async Task Maps_a_401_to_an_authentication_failure()
    {
        var (client, stub) = Build();
        stub.RespondWith(HttpStatusCode.Unauthorized, """{"error":"not authenticated"}""");

        var ex = await Assert.ThrowsAsync<IbkrAuthenticationException>(
            () => client.SendAsync<Account>(
                IbkrRequest.Get("/v1/api/iserver/accounts"),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("/v1/api/iserver/accounts", ex.Path);
        Assert.Contains("not authenticated", ex.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Maps_a_429_to_a_rate_limit_failure_and_reads_retry_after()
    {
        var (client, stub) = Build();
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"too many requests"}"""),
        };
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        stub.RespondWith(response);

        var ex = await Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => client.SendAsync<Account>(
                IbkrRequest.Get("/v1/api/iserver/accounts"),
                TestContext.Current.CancellationToken));

        Assert.Equal(Duration.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task Reports_the_request_and_status_when_a_call_fails()
    {
        var (client, stub) = Build();
        stub.RespondWith(HttpStatusCode.ServiceUnavailable, "gateway down");

        var ex = await Assert.ThrowsAsync<IbkrApiException>(
            () => client.SendAsync<Account>(
                IbkrRequest.Get("/v1/api/iserver/accounts"),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal("GET", ex.Method);
        Assert.Contains("gateway down", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explains_an_unreadable_body_instead_of_leaking_a_json_exception()
    {
        var (client, stub) = Build();
        stub.RespondWithJson("this is not json");

        var ex = await Assert.ThrowsAsync<IbkrApiException>(
            () => client.SendAsync<Account>(
                IbkrRequest.Get("/v1/api/iserver/accounts"),
                TestContext.Current.CancellationToken));

        Assert.Contains("could not be read as Account", ex.Message, StringComparison.Ordinal);
        Assert.Equal("this is not json", ex.ResponseBody);
    }

    [Fact]
    public async Task Treats_an_empty_success_body_as_a_failure_when_a_value_was_expected()
    {
        var (client, stub) = Build();
        stub.RespondWith(HttpStatusCode.OK, string.Empty);

        var ex = await Assert.ThrowsAsync<IbkrApiException>(
            () => client.SendAsync<Account>(
                IbkrRequest.Get("/v1/api/iserver/accounts"),
                TestContext.Current.CancellationToken));

        Assert.Contains("empty body", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accepts_an_empty_body_when_no_value_was_expected()
    {
        var (client, stub) = Build();
        stub.RespondWith(HttpStatusCode.OK, string.Empty);

        await client.SendAsync(
            IbkrRequest.Post("/v1/api/logout"),
            TestContext.Current.CancellationToken);

        Assert.Single(stub.Requests);
    }
}
