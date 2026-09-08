using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Authentication;

public class AuthenticationHandlerTests
{
    private sealed class RecordingAuthenticator(bool requiresCookie) : IIbkrAuthenticator
    {
        public int Invocations { get; private set; }

        public string Scheme => "Recording";

        public bool RequiresSessionCookie { get; } = requiresCookie;

        public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            request.Headers.Add("X-Test-Credential", "applied");
            return ValueTask.CompletedTask;
        }
    }

    private static (HttpClient Client, StubHttpMessageHandler Stub) Build(
        IIbkrAuthenticator authenticator,
        IbkrSessionState state,
        IbkrTradingOptions? options = null)
    {
        var stub = new StubHttpMessageHandler();
        var handler = new IbkrAuthenticationHandler(
            authenticator,
            state,
            Options.Create(options ?? new IbkrTradingOptions()))
        {
            InnerHandler = stub,
        };

        return (new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5000") }, stub);
    }

    [Fact]
    public async Task Invokes_the_authenticator_for_every_request()
    {
        var authenticator = new RecordingAuthenticator(requiresCookie: false);
        var (client, stub) = Build(authenticator, new IbkrSessionState());

        await client.GetAsync(new Uri("/v1/api/iserver/accounts", UriKind.Relative), TestContext.Current.CancellationToken);
        await client.GetAsync(new Uri("/v1/api/portfolio/accounts", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(2, authenticator.Invocations);
        Assert.Equal("applied", stub.LastRequest.Headers["X-Test-Credential"]);
    }

    [Fact]
    public async Task Sets_a_user_agent_because_ibkr_asks_every_client_to_identify_itself()
    {
        var options = new IbkrTradingOptions { UserAgent = "contoso-trader/2.1" };
        var (client, stub) = Build(new RecordingAuthenticator(false), new IbkrSessionState(), options);

        await client.GetAsync(new Uri("/v1/api/tickle", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("contoso-trader/2.1", stub.LastRequest.Headers["User-Agent"]);
    }

    [Fact]
    public async Task Attaches_the_session_cookie_when_the_mechanism_requires_it()
    {
        var state = new IbkrSessionState();
        state.Set("c8fh17fnjr01hfnrh39rhfh8shd1hd93", Instant.FromUtc(2024, 1, 1, 0, 0));
        var (client, stub) = Build(new RecordingAuthenticator(requiresCookie: true), state);

        await client.GetAsync(new Uri("/v1/api/iserver/accounts", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("api=c8fh17fnjr01hfnrh39rhfh8shd1hd93", stub.LastRequest.Headers["Cookie"]);
    }

    [Fact]
    public async Task Does_not_attach_a_session_cookie_for_the_gateway_which_manages_its_own()
    {
        var state = new IbkrSessionState();
        state.Set("token", Instant.FromUtc(2024, 1, 1, 0, 0));
        var (client, stub) = Build(ClientPortalGatewayAuthenticator.Instance, state);

        await client.GetAsync(new Uri("/v1/api/iserver/accounts", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.False(stub.LastRequest.Headers.ContainsKey("Cookie"));
    }

    [Fact]
    public async Task Sends_no_cookie_before_a_session_token_has_been_obtained()
    {
        var (client, stub) = Build(new RecordingAuthenticator(requiresCookie: true), new IbkrSessionState());

        await client.GetAsync(new Uri("/v1/api/tickle", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.False(stub.LastRequest.Headers.ContainsKey("Cookie"));
    }

    [Fact]
    public void Clearing_session_state_discards_the_token_as_ibkr_requires_on_logout()
    {
        var state = new IbkrSessionState();
        state.Set("token", Instant.FromUtc(2024, 1, 1, 0, 0));

        state.Clear();

        Assert.Null(state.SessionToken);
        Assert.Null(state.ObtainedAt);
    }
}
