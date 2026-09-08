using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Session;

namespace IbkrDotNet.Trading;

/// <summary>
/// The entry point to the Interactive Brokers Trading Web API.
/// </summary>
/// <remarks>
/// Groups the endpoint clients so a single dependency reaches all of them. Inject an individual
/// client such as <see cref="IOrdersClient"/> instead where a component only needs one.
/// </remarks>
public interface IIbkrTradingClient
{
    /// <summary>The brokerage session lifecycle.</summary>
    IIbkrSessionManager Session { get; }

    /// <summary>The session endpoints.</summary>
    ISessionClient Sessions { get; }

    /// <summary>The account endpoints.</summary>
    IAccountsClient Accounts { get; }

    /// <summary>The portfolio endpoints.</summary>
    IPortfolioClient Portfolio { get; }

    /// <summary>The contract and instrument discovery endpoints.</summary>
    IContractsClient Contracts { get; }

    /// <summary>The order endpoints.</summary>
    IOrdersClient Orders { get; }

    /// <summary>The market data endpoints.</summary>
    IMarketDataClient MarketData { get; }
}

/// <inheritdoc cref="IIbkrTradingClient" />
public sealed class IbkrTradingClient : IIbkrTradingClient
{
    /// <summary>Creates the client from its endpoint clients.</summary>
    /// <param name="session">The brokerage session manager.</param>
    /// <param name="sessions">The session client.</param>
    /// <param name="accounts">The accounts client.</param>
    /// <param name="portfolio">The portfolio client.</param>
    /// <param name="contracts">The contracts client.</param>
    /// <param name="orders">The orders client.</param>
    /// <param name="marketData">The market data client.</param>
    public IbkrTradingClient(
        IIbkrSessionManager session,
        ISessionClient sessions,
        IAccountsClient accounts,
        IPortfolioClient portfolio,
        IContractsClient contracts,
        IOrdersClient orders,
        IMarketDataClient marketData)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(marketData);

        Session = session;
        Sessions = sessions;
        Accounts = accounts;
        Portfolio = portfolio;
        Contracts = contracts;
        Orders = orders;
        MarketData = marketData;
    }

    /// <inheritdoc />
    public IIbkrSessionManager Session { get; }

    /// <inheritdoc />
    public ISessionClient Sessions { get; }

    /// <inheritdoc />
    public IAccountsClient Accounts { get; }

    /// <inheritdoc />
    public IPortfolioClient Portfolio { get; }

    /// <inheritdoc />
    public IContractsClient Contracts { get; }

    /// <inheritdoc />
    public IOrdersClient Orders { get; }

    /// <inheritdoc />
    public IMarketDataClient MarketData { get; }
}
