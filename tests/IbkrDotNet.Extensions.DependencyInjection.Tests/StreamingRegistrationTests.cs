using System.Net.WebSockets;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Session;
using IbkrDotNet.Trading.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Extensions.DependencyInjection.Tests;

/// <summary>
/// The socket is wired the way the HTTP client is, and nothing here reaches a network: the
/// connector is substituted with one that records the upgrade request and refuses it.
/// </summary>
public class StreamingRegistrationTests
{
    private static ServiceProvider Build(RecordingConnector connector, Action<IbkrTradingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Both registered ahead of AddIbkrTrading, which uses TryAdd for each.
        services.AddSingleton<IIbkrWebSocketConnector>(connector);
        services.AddSingleton<IIbkrSessionManager>(provider =>
            new ReadySessionManager(provider.GetRequiredService<IbkrSessionState>()));

        services.AddIbkrTrading(configure);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void Registers_one_transport_shared_by_the_facade_and_the_stream_client()
    {
        using var provider = Build(new RecordingConnector());

        var transport = provider.GetRequiredService<IIbkrStreamingTransport>();
        var facade = provider.GetRequiredService<IIbkrTradingClient>();

        Assert.IsType<IbkrStreamingTransport>(transport);
        Assert.Same(transport, provider.GetRequiredService<IIbkrStreamingTransport>());
        Assert.Same(transport, facade.Streaming);
        Assert.IsType<MarketDataStreamClient>(facade.MarketDataStream);
        Assert.Equal(StreamingConnectionState.Disconnected, transport.State);
    }

    [Fact]
    public void Uses_the_real_connector_unless_the_host_substitutes_one()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrTrading();
        using var provider = services.BuildServiceProvider();

        Assert.Same(ClientWebSocketConnector.Instance, provider.GetRequiredService<IIbkrWebSocketConnector>());
    }

    [Fact]
    public async Task Opens_the_socket_under_the_base_address_with_the_session_cookie()
    {
        var connector = new RecordingConnector();
        await using var provider = Build(connector, o => o.BaseAddress = new Uri("https://localhost:5050"));
        var transport = provider.GetRequiredService<IIbkrStreamingTransport>();

        await Assert.ThrowsAsync<IbkrStreamingException>(
            () => transport.ConnectAsync(TestContext.Current.CancellationToken));

        var request = Assert.Single(connector.Requests);
        Assert.Equal(new Uri("wss://localhost:5050/v1/api/ws"), request.Address);
        Assert.Equal($"api={ReadySessionManager.Token}", request.Headers["Cookie"]);
        Assert.Equal("https://localhost:5050", request.Headers["Origin"]);
        Assert.Equal(IbkrTradingOptions.DefaultUserAgent, request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task Hands_the_web_socket_hook_to_the_connector()
    {
        var connector = new RecordingConnector();
        static void Configure(ClientWebSocketOptions options) => options.SetRequestHeader("X-Test", "1");
        await using var provider = Build(connector, o => o.Streaming.ConfigureClientWebSocket = Configure);
        var transport = provider.GetRequiredService<IIbkrStreamingTransport>();

        await Assert.ThrowsAsync<IbkrStreamingException>(
            () => transport.ConnectAsync(TestContext.Current.CancellationToken));

        Assert.Same((Action<ClientWebSocketOptions>)Configure, Assert.Single(connector.Requests).Configure);
    }

    [Fact]
    public async Task The_stream_client_reads_over_the_registered_transport()
    {
        var connector = new RecordingConnector();
        await using var provider = Build(connector);
        var client = provider.GetRequiredService<IIbkrTradingClient>();

        var stream = client.MarketDataStream.SubscribeAsync(
            new ConId(8314),
            cancellationToken: TestContext.Current.CancellationToken);

        // Nothing is sent until the first read, and the first read opens the socket.
        Assert.Empty(connector.Requests);
        await Assert.ThrowsAsync<IbkrStreamingException>(async () =>
        {
            await foreach (var _ in stream)
            {
            }
        });

        Assert.Single(connector.Requests);
    }

    [Fact]
    public async Task Fails_at_startup_when_a_streaming_option_is_out_of_range()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIbkrTrading(o => o.Streaming.KeepAliveInterval = Duration.Zero);
        using var host = builder.Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("KeepAliveInterval", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disposing_the_container_disposes_the_transport()
    {
        var provider = Build(new RecordingConnector());
        var transport = provider.GetRequiredService<IIbkrStreamingTransport>();

        await provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.ConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_container_disposed_synchronously_still_disposes_the_transport()
    {
        // ServiceProvider.Dispose refuses a service that only implements IAsyncDisposable, which
        // is what a bare service collection in a test, or a console application, runs into.
        var provider = Build(new RecordingConnector());
        var transport = provider.GetRequiredService<IIbkrStreamingTransport>();

        provider.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.ConnectAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Records each upgrade request and refuses it, the way an unreachable gateway would.</summary>
    private sealed class RecordingConnector : IIbkrWebSocketConnector
    {
        private readonly List<StreamingConnectRequest> _requests = [];

        public IReadOnlyList<StreamingConnectRequest> Requests => _requests;

        public Task<WebSocket> ConnectAsync(StreamingConnectRequest request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            throw new WebSocketException("No network in this test.");
        }
    }

    /// <summary>A session that is always ready, recording the token the way a real tickle would.</summary>
    private sealed class ReadySessionManager(IbkrSessionState state) : IIbkrSessionManager
    {
        /// <summary>The session token from IBKR's own WebSocket walkthrough.</summary>
        public const string Token = "d21b8cf5ebc8ea01c6ce37c8125ec83f";

        public Task<BrokerageSessionStatus> EnsureBrokerageSessionAsync(CancellationToken cancellationToken = default)
        {
            state.Set(Token, SystemClock.Instance.GetCurrentInstant());
            return Task.FromResult(new BrokerageSessionStatus { Connected = true, Authenticated = true, Established = true });
        }

        public Task<TickleResponse> KeepAliveAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
