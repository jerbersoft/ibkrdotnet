using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Registers the Interactive Brokers Trading client with a service collection.
/// </summary>
public static class IbkrTradingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Trading client, its endpoint clients and its HTTP pipeline.
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
            // Fail at startup rather than at the first request, when a misconfiguration is far more
            // expensive to diagnose.
            .ValidateOnStart();

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
        services.AddTransient<IbkrRateLimitHandler>();

        services
            .AddHttpClient(IbkrApiClient.HttpClientName, ConfigureHttpClient)
            // Rate limiting runs outermost so a request waits before anything else touches it, and
            // authentication runs closest to the wire so a signature is minted at send time.
            .AddHttpMessageHandler<IbkrRateLimitHandler>()
            .AddHttpMessageHandler<IbkrAuthenticationHandler>();

        // The factory is passed in rather than a resolved client: a singleton holding one client
        // would pin the handler chain created at startup and never see a DNS change.
        services.TryAddSingleton<IIbkrApiClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new IbkrApiClient(
                () => factory.CreateClient(IbkrApiClient.HttpClientName),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IbkrApiClient>>());
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
        services.TryAddSingleton<IIbkrSessionManager, IbkrSessionManager>();
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
    ///     "MaxWait": "00:00:30"
    ///   }
    /// }
    /// </code>
    /// <para>
    /// Durations accept a <c>TimeSpan</c> string, NodaTime's round-trip form, or a whole number of
    /// seconds. They are bound explicitly rather than by reflection because
    /// <see cref="Duration"/> is not a type the configuration binder can construct.
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
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IbkrTradingOptions>>()
            .Value;

        client.BaseAddress = options.ResolveBaseAddress();
        client.Timeout = options.Timeout.ToTimeSpan();
    }
}
