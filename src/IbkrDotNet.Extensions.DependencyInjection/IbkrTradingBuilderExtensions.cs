using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Authentication.OAuth1a;
using IbkrDotNet.Trading.Authentication.OAuth2;
using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Selects an authentication mechanism and optional behaviours for the Trading client.
/// </summary>
public static class IbkrTradingBuilderExtensions
{
    /// <summary>
    /// Authenticates through a locally running Client Portal Gateway. This is the default.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IIbkrTradingBuilder UseClientPortalGateway(this IIbkrTradingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Replace(builder, _ => ClientPortalGatewayAuthenticator.Instance);
        return builder;
    }

    /// <summary>
    /// Authenticates directly against IBKR using OAuth 2.0.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Configures the OAuth 2.0 credentials.</param>
    /// <remarks>
    /// The token exchanges run on their own <see cref="HttpClient"/>, outside the authenticating
    /// handler; routing them through it would recurse.
    /// </remarks>
    public static IIbkrTradingBuilder UseOAuth2(
        this IIbkrTradingBuilder builder,
        Action<OAuth2Options> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<OAuth2Options>().Configure(configure);
        AddUnauthenticatedClient(builder, OAuth2Authenticator.HttpClientName);

        Replace(builder, provider => new OAuth2Authenticator(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(OAuth2Authenticator.HttpClientName),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OAuth2Options>>(),
            provider.GetRequiredService<IClock>()));

        return builder;
    }

    /// <summary>
    /// Authenticates directly against IBKR using OAuth 1.0a.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Configures the OAuth 1.0a credentials.</param>
    /// <remarks>
    /// The live session token handshake runs on its own <see cref="HttpClient"/>, outside the
    /// authenticating handler; routing it through it would recurse.
    /// </remarks>
    public static IIbkrTradingBuilder UseOAuth1a(
        this IIbkrTradingBuilder builder,
        Action<OAuth1aOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<OAuth1aOptions>().Configure(configure);
        AddUnauthenticatedClient(builder, OAuth1aAuthenticator.HttpClientName);

        Replace(builder, provider => new OAuth1aAuthenticator(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(OAuth1aAuthenticator.HttpClientName),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OAuth1aOptions>>(),
            provider.GetRequiredService<IClock>()));

        return builder;
    }

    /// <summary>
    /// Establishes the brokerage session at startup and keeps it alive in the background.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Configures the keep-alive interval and startup behaviour.</param>
    /// <remarks>
    /// An IBKR session times out after a few idle minutes, so a long-running application needs
    /// something pinging <c>/tickle</c>. Without this, the session must be kept alive by hand
    /// through <see cref="Trading.Session.IIbkrSessionManager"/>.
    /// </remarks>
    public static IIbkrTradingBuilder AddBrokerageSessionKeepAlive(
        this IIbkrTradingBuilder builder,
        Action<BrokerageSessionKeepAliveOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Services.AddOptions<BrokerageSessionKeepAliveOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.Validate(
            o => o.Interval > Duration.Zero,
            $"{nameof(BrokerageSessionKeepAliveOptions.Interval)} must be positive.");

        builder.Services.AddHostedService<BrokerageSessionKeepAlive>();
        return builder;
    }

    private static void AddUnauthenticatedClient(IIbkrTradingBuilder builder, string name) =>
        builder.Services.AddHttpClient(name, (provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<IbkrTradingOptions>>()
                .Value;

            client.BaseAddress = options.ResolveBaseAddress();
            client.Timeout = options.Timeout.ToTimeSpan();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        });

    private static void Replace(
        IIbkrTradingBuilder builder,
        Func<IServiceProvider, IIbkrAuthenticator> factory)
    {
        builder.Services.RemoveAll<IIbkrAuthenticator>();
        builder.Services.AddSingleton(factory);
    }
}
