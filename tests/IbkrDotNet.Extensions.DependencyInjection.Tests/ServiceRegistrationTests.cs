using System.Security.Cryptography;
using IbkrDotNet.Extensions.DependencyInjection;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Authentication.OAuth1a;
using IbkrDotNet.Trading.Authentication.OAuth2;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace IbkrDotNet.Extensions.DependencyInjection.Tests;

public class ServiceRegistrationTests
{
    private static ServiceProvider Build(Action<IIbkrTradingBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddIbkrTrading();
        configure?.Invoke(builder);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Theory]
    [InlineData(typeof(IIbkrTradingClient))]
    [InlineData(typeof(IIbkrSessionManager))]
    [InlineData(typeof(IIbkrApiClient))]
    [InlineData(typeof(IIbkrAuthenticator))]
    [InlineData(typeof(ISessionClient))]
    [InlineData(typeof(IAccountsClient))]
    [InlineData(typeof(IPortfolioClient))]
    [InlineData(typeof(IContractsClient))]
    [InlineData(typeof(IOrdersClient))]
    [InlineData(typeof(IMarketDataClient))]
    [InlineData(typeof(IWatchlistsClient))]
    [InlineData(typeof(IScannerClient))]
    [InlineData(typeof(INotificationsClient))]
    [InlineData(typeof(IClock))]
    [InlineData(typeof(IDateTimeZoneProvider))]
    [InlineData(typeof(IbkrSessionState))]
    public void Every_registered_service_resolves(Type serviceType)
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void Exposes_every_endpoint_client_through_the_facade()
    {
        using var provider = Build();

        var client = provider.GetRequiredService<IIbkrTradingClient>();

        Assert.Same(provider.GetRequiredService<IOrdersClient>(), client.Orders);
        Assert.Same(provider.GetRequiredService<IPortfolioClient>(), client.Portfolio);
        Assert.Same(provider.GetRequiredService<IIbkrSessionManager>(), client.Session);
    }

    [Fact]
    public void Defaults_to_the_client_portal_gateway()
    {
        using var provider = Build();

        var authenticator = provider.GetRequiredService<IIbkrAuthenticator>();

        Assert.IsType<ClientPortalGatewayAuthenticator>(authenticator);
        Assert.False(authenticator.RequiresSessionCookie);
    }

    [Fact]
    public void Points_the_http_client_at_the_gateway_by_default()
    {
        using var provider = Build();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(IbkrApiClient.HttpClientName);

        Assert.Equal(new Uri("https://localhost:5000"), client.BaseAddress);
    }

    [Fact]
    public void Points_the_http_client_at_production_when_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrTrading(o => o.Environment = IbkrEnvironment.Production);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(IbkrApiClient.HttpClientName);

        Assert.Equal(new Uri("https://api.ibkr.com"), client.BaseAddress);
    }

    [Fact]
    public void Replaces_the_gateway_when_oauth2_is_selected()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();

        using var provider = Build(builder => builder.UseOAuth2(o =>
        {
            o.ClientId = "TESTCLIENT";
            o.ClientKeyId = "key-1";
            o.Credential = "testuser";
            o.ClientIpAddress = "203.0.113.7";
            o.UsePrivateKeyPem(pem);
        }));

        var authenticator = provider.GetRequiredService<IIbkrAuthenticator>();

        Assert.IsType<OAuth2Authenticator>(authenticator);
        Assert.True(authenticator.RequiresSessionCookie);
        Assert.Single(provider.GetServices<IIbkrAuthenticator>());
    }

    [Fact]
    public void Replaces_the_gateway_when_oauth1a_is_selected()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();

        using var provider = Build(builder => builder.UseOAuth1a(o =>
        {
            o.ConsumerKey = "TESTCONS";
            o.Realm = OAuth1aOptions.TestRealm;
            o.AccessToken = "token";
            o.AccessTokenSecret = "c2VjcmV0";
            o.DiffieHellmanPrime = "f7e75fdc469067ffdc4e847c51f452df";
            o.UseEncryptionKeyPem(pem);
            o.UseSignatureKeyPem(pem);
        }));

        Assert.IsType<OAuth1aAuthenticator>(provider.GetRequiredService<IIbkrAuthenticator>());
    }

    [Fact]
    public void Gives_the_oauth_token_exchange_its_own_client_so_it_cannot_recurse()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();

        using var provider = Build(builder => builder.UseOAuth2(o =>
        {
            o.ClientId = "TESTCLIENT";
            o.ClientKeyId = "key-1";
            o.Credential = "testuser";
            o.ClientIpAddress = "203.0.113.7";
            o.UsePrivateKeyPem(pem);
        }));

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var oauthClient = factory.CreateClient(OAuth2Authenticator.HttpClientName);

        Assert.Equal(new Uri("https://localhost:5000"), oauthClient.BaseAddress);
        Assert.NotEqual(IbkrApiClient.HttpClientName, OAuth2Authenticator.HttpClientName);
    }

    [Fact]
    public void Lets_a_host_substitute_the_clock()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0)));
        services.AddIbkrTrading();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeClock>(provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void Registers_the_keep_alive_only_when_asked()
    {
        using var without = Build();
        using var with = Build(builder => builder.AddBrokerageSessionKeepAlive());

        Assert.Empty(without.GetServices<IHostedService>());
        Assert.Single(with.GetServices<IHostedService>());
    }

    [Fact]
    public async Task Fails_at_startup_rather_than_at_the_first_request_when_options_are_invalid()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIbkrTrading(o => o.UserAgent = string.Empty);
        using var host = builder.Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("UserAgent", ex.Message, StringComparison.Ordinal);
    }
}

public class ConfigurationBindingTests
{
    private static IbkrTradingOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrTrading(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<IbkrTradingOptions>>().Value;
    }

    [Fact]
    public void Binds_the_documented_keys()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Environment"] = "Production",
            ["UserAgent"] = "contoso-trader/1.0",
            ["Timeout"] = "00:00:45",
            ["RateLimiting:Enabled"] = "false",
            ["RateLimiting:EnforceGlobalLimit"] = "false",
            ["RateLimiting:MaxWait"] = "90",
        });

        Assert.Equal(IbkrEnvironment.Production, options.Environment);
        Assert.Equal("contoso-trader/1.0", options.UserAgent);
        Assert.Equal(Duration.FromSeconds(45), options.Timeout);
        Assert.False(options.RateLimiting.Enabled);
        Assert.False(options.RateLimiting.EnforceGlobalLimit);

        // A bare number is read as seconds.
        Assert.Equal(Duration.FromSeconds(90), options.RateLimiting.MaxWait);
    }

    [Fact]
    public void Leaves_defaults_alone_for_keys_that_are_absent()
    {
        var options = Bind([]);

        Assert.Equal(IbkrEnvironment.ClientPortalGateway, options.Environment);
        Assert.Equal(Duration.FromSeconds(30), options.Timeout);
        Assert.True(options.RateLimiting.Enabled);
    }

    [Fact]
    public void An_explicit_base_address_overrides_the_environment()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Environment"] = "Production",
            ["BaseAddress"] = "https://localhost:5001",
        });

        Assert.Equal(new Uri("https://localhost:5001"), options.ResolveBaseAddress());
    }

    [Fact]
    public void Rejects_a_base_address_that_is_not_a_uri()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Bind(new Dictionary<string, string?> { ["BaseAddress"] = "not a uri" }));

        Assert.Contains("BaseAddress", ex.Message, StringComparison.Ordinal);
    }
}
