using Microsoft.Extensions.DependencyInjection;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Continues the configuration of the Interactive Brokers Trading client after
/// <see cref="IbkrTradingServiceCollectionExtensions.AddIbkrTrading(IServiceCollection, Action{IbkrDotNet.Trading.Configuration.IbkrTradingOptions}?)"/>.
/// </summary>
public interface IIbkrTradingBuilder
{
    /// <summary>The service collection the client was registered into.</summary>
    IServiceCollection Services { get; }
}

internal sealed class IbkrTradingBuilder(IServiceCollection services) : IIbkrTradingBuilder
{
    public IServiceCollection Services { get; } = services
        ?? throw new ArgumentNullException(nameof(services));
}
