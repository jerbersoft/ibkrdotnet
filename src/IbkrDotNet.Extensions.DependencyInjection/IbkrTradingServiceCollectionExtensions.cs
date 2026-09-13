using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Session;
using IbkrDotNet.Trading.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Registers the Interactive Brokers Trading client with a service collection.
/// </summary>
public static class IbkrTradingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Trading client, its endpoint clients, its HTTP pipeline and the WebSocket
    /// transport the streaming topics run over.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the client options.</param>
    /// <returns>A builder for selecting an authentication mechanism and further options.</returns>
    /// <remarks>
    /// Authentication defaults to the Client Portal Gateway. Call
    /// <see cref="IbkrTradingBuilderExtensions.UseOAuth2"/> or
    /// <see cref="IbkrTradingBuilderExtensions.UseOAuth1a"/> on the returned builder to talk to
    /// <c>https://api.ibkr.com</c> directly instead.
    /// </remarks>
    public static IIbkrTradingBuilder AddIbkrTrading(
        this IServiceCollection services,
        Action<IbkrTradingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<IbkrTradingOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                $"{nameof(IbkrTradingOptions.UserAgent)} is required; IBKR asks every client to identify itself.")
            .Validate(
                options => options.Timeout > Duration.Zero,
                $"{nameof(IbkrTradingOptions.Timeout)} must be positive.")
            .Validate(
                options => options.RateLimiting.MaxWait >= Duration.Zero,
                $"{nameof(IbkrRateLimitingOptions.MaxWait)} cannot be negative.")
            .Validate(
                options => options.RateLimiting.DefaultRetryAfter >= Duration.Zero,
                $"{nameof(IbkrRateLimitingOptions.DefaultRetryAfter)} cannot be negative.")
            // Fail at startup rather than at the first request, when a misconfiguration is far more
            // expensive to diagnose.
            .ValidateOnStart();

        // The streaming options carry their own rules, shared with the transport's constructor, so
        // they are run as a validator rather than restated here.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<IbkrTradingOptions>, IbkrStreamingOptionsValidator>());

        // Registered with TryAdd so a host can substitute a FakeClock or a fixed zone provider.
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.TryAddSingleton(DateTimeZoneProviders.Tzdb);
        services.TryAddSingleton<IbkrSessionState>();

        // The limiters are a singleton: IHttpClientFactory rotates handler chains on its own
        // lifetime, and holding the windows in the handler would reset them on every rotation.
        services.TryAddSingleton<IbkrRateLimiterRegistry>();

        // The gateway is the default; UseOAuth2 and UseOAuth1a replace it.
        services.TryAddSingleton<IIbkrAuthenticator>(ClientPortalGatewayAuthenticator.Instance);

        services.AddTransient<IbkrAuthenticationHandler>();

        services
            .AddHttpClient(IbkrApiClient.HttpClientName, ConfigureHttpClient)
            // Authentication runs closest to the wire so a signature is minted at send time.
            // Rate limiting is deliberately not a handler here: HttpClient.Timeout covers the whole
            // chain, so a request paced inside it spends the caller's budget waiting and then fails
            // as a timeout having never been sent. IbkrApiClient paces before the send instead.
            .AddHttpMessageHandler<IbkrAuthenticationHandler>();

        // The factory is passed in rather than a resolved client: a singleton holding one client
        // would pin the handler chain created at startup and never see a DNS change.
        services.TryAddSingleton<IIbkrApiClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new IbkrApiClient(
                () => factory.CreateClient(IbkrApiClient.HttpClientName),
                provider.GetRequiredService<IbkrRateLimiterRegistry>(),
                provider.GetRequiredService<ILogger<IbkrApiClient>>());
        });

        services.TryAddSingleton<ISessionClient, SessionClient>();
        services.TryAddSingleton<IAccountsClient, AccountsClient>();
        services.TryAddSingleton<IPortfolioClient, PortfolioClient>();
        services.TryAddSingleton<IContractsClient, ContractsClient>();
        services.TryAddSingleton<IOrdersClient, OrdersClient>();
        services.TryAddSingleton<IMarketDataClient, MarketDataClient>();
        services.TryAddSingleton<IWatchlistsClient, WatchlistsClient>();
        services.TryAddSingleton<IScannerClient, ScannerClient>();
        services.TryAddSingleton<INotificationsClient, NotificationsClient>();
        services.TryAddSingleton<IEventContractsClient, EventContractsClient>();
        services.TryAddSingleton<IAlertsClient, AlertsClient>();
        services.TryAddSingleton<IPortfolioAnalystClient, PortfolioAnalystClient>();
        services.TryAddSingleton<IIbkrSessionManager, IbkrSessionManager>();

        // One socket per session, shared by every streaming topic, and opened only when a stream is
        // first read. The connector is its own registration so a host, or a test, can substitute one
        // that opens no network socket. Disposing the container closes the socket.
        services.TryAddSingleton<IIbkrWebSocketConnector>(ClientWebSocketConnector.Instance);
        services.TryAddSingleton<IIbkrStreamingTransport>(provider => new IbkrStreamingTransport(
            provider.GetRequiredService<IIbkrSessionManager>(),
            provider.GetRequiredService<IbkrSessionState>(),
            provider.GetRequiredService<IIbkrAuthenticator>(),
            provider.GetRequiredService<IIbkrWebSocketConnector>(),
            provider.GetRequiredService<IOptions<IbkrTradingOptions>>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<IbkrStreamingTransport>>()));
        services.TryAddSingleton<IMarketDataStreamClient, MarketDataStreamClient>();

        services.TryAddSingleton<IIbkrTradingClient, IbkrTradingClient>();

        return new IbkrTradingBuilder(services);
    }

    /// <summary>
    /// Registers the Trading client, binding its options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section holding the client settings.</param>
    /// <remarks>
    /// <para>Recognised keys, all optional:</para>
    /// <code>
    /// {
    ///   "Environment": "ClientPortalGateway",   // or Production, Sandbox
    ///   "BaseAddress": "https://localhost:5001",
    ///   "UserAgent": "contoso-trader/1.0",
    ///   "Timeout": "00:00:30",
    ///   "RateLimiting": {
    ///     "Enabled": true,
    ///     "EnforceGlobalLimit": true,
    ///     "MaxWait": "00:00:30",
    ///     "DefaultRetryAfter": "00:00:05"
    ///   },
    ///   "Streaming": {
    ///     "Address": "wss://localhost:5001/v1/api/ws",
    ///     "Origin": "https://localhost:5001",
    ///     "KeepAliveInterval": "00:00:30",
    ///     "Reconnect": true,
    ///     "ReconnectDelay": "00:00:01",
    ///     "ReconnectMaxDelay": "00:00:30",
    ///     "BufferCapacity": 1,
    ///     "Overflow": "DropOldest",              // or DropNewest
    ///     "CloseTimeout": "00:00:05",
    ///     "MarketDataRenewalInterval": "00:10:00"
    ///   }
    /// }
    /// </code>
    /// <para>
    /// Durations accept a <c>TimeSpan</c> string, NodaTime's round-trip form, or a whole number of
    /// seconds. They are bound explicitly rather than by reflection because
    /// <see cref="Duration"/> is not a type the configuration binder can construct.
    /// </para>
    /// <para>
    /// <c>Streaming.ConfigureClientWebSocket</c> is a delegate and cannot come from configuration;
    /// set it in code through the other overload or <c>services.Configure&lt;IbkrTradingOptions&gt;</c>.
    /// </para>
    /// </remarks>
    public static IIbkrTradingBuilder AddIbkrTrading(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddIbkrTrading(options => IbkrTradingConfigurationBinder.Bind(options, configuration));
    }

    private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<IbkrTradingOptions>>().Value;

        client.BaseAddress = options.ResolveBaseAddress();
        client.Timeout = options.Timeout.ToTimeSpan();
    }
}
