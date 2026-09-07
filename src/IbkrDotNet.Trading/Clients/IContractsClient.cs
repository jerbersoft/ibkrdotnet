using IbkrDotNet.Trading.Models.Contracts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Trading Contracts endpoints: finding instruments and the rules governing orders for them.
/// </summary>
public interface IContractsClient
{
    /// <summary>Searches instruments by symbol or company name.</summary>
    /// <param name="symbol">The symbol or name to search for.</param>
    /// <param name="securityType">The underlying's security type: <c>STK</c>, <c>IND</c> or <c>BOND</c>.</param>
    /// <param name="searchByName">Whether <paramref name="symbol"/> is a company name rather than a symbol.</param>
    /// <param name="more">Whether to return additional results.</param>
    /// <param name="fund">Whether to include funds.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<ContractSearchResult>> SearchAsync(
        string symbol,
        string? securityType = null,
        bool? searchByName = null,
        bool? more = null,
        bool? fund = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns instrument attributes for a contract, or for an option on one.</summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="securityType">The security type to resolve.</param>
    /// <param name="month">The contract month, for derivatives.</param>
    /// <param name="exchange">The exchange.</param>
    /// <param name="strike">The strike, for options.</param>
    /// <param name="right">The option right: <c>C</c> or <c>P</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<InstrumentAttributes>> GetAttributesAsync(
        ConId conId,
        string? securityType = null,
        string? month = null,
        string? exchange = null,
        string? strike = null,
        string? right = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the strikes available on an underlying.</summary>
    /// <param name="conId">The underlying instrument.</param>
    /// <param name="securityType">The derivative type: <c>OPT</c>, <c>FOP</c> or <c>WAR</c>.</param>
    /// <param name="month">The contract month, for example <c>JAN25</c>.</param>
    /// <param name="exchange">The exchange. Defaults to <c>SMART</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<OptionStrikes> GetStrikesAsync(
        ConId conId,
        string securityType,
        string month,
        string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the filters available when searching an issuer's bonds.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="issuerId">The issuer.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<BondFilters> GetBondFiltersAsync(
        string symbol,
        string issuerId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns general information about an instrument.</summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<InstrumentInfo> GetInfoAsync(ConId conId, CancellationToken cancellationToken = default);

    /// <summary>Returns instrument information together with its market rules.</summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<InstrumentInfo> GetInfoAndRulesAsync(ConId conId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the market rules governing orders for an instrument.
    /// </summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="isBuy">Which side of the market the rules apply to.</param>
    /// <param name="modifyOrder">Whether the rules are for modifying an existing order.</param>
    /// <param name="orderId">The order being modified, when <paramref name="modifyOrder"/> is set.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Modification is subject to a different ruleset from submission, so pass
    /// <paramref name="modifyOrder"/> when inspecting what a change to a working order may do.
    /// </remarks>
    Task<ContractRules> GetRulesAsync(
        ConId conId,
        bool isBuy = true,
        bool modifyOrder = false,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the execution algorithms available for an instrument.</summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="algorithmIds">Up to eight algorithm ids to filter by. Case sensitive.</param>
    /// <param name="includeDescriptions">Whether to include algorithm descriptions.</param>
    /// <param name="includeParameters">Whether to include algorithm parameters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<InstrumentAlgorithms> GetAlgorithmsAsync(
        ConId conId,
        IEnumerable<string>? algorithmIds = null,
        bool includeDescriptions = false,
        bool includeParameters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns definitions for a set of instruments.
    /// </summary>
    /// <param name="conIds">The instruments. IBKR accepts at most 200 per request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">More than 200 contract identifiers were supplied.</exception>
    Task<InstrumentDefinitions> GetDefinitionsAsync(
        IEnumerable<ConId> conIds,
        CancellationToken cancellationToken = default);

    /// <summary>Returns stock listings by symbol, keyed by the symbol requested.</summary>
    /// <param name="symbols">The symbols to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<StockListing>>> GetStocksAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>Returns futures contracts by underlying symbol, keyed by the symbol requested.</summary>
    /// <param name="symbols">The underlying symbols to look up.</param>
    /// <param name="exchange">An exchange to restrict the results to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<FutureContract>>> GetFuturesAsync(
        IEnumerable<string> symbols,
        string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every instrument of an asset class on an exchange.</summary>
    /// <param name="exchange">The exchange.</param>
    /// <param name="assetClass">The asset class. Defaults to <c>STK</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<ExchangeListing>> GetListingsByExchangeAsync(
        string exchange,
        string? assetClass = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the trading schedules for a symbol.
    /// </summary>
    /// <param name="assetClass">The asset class, for example <c>STK</c>.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="exchange">The exchange.</param>
    /// <param name="exchangeFilter">Exchanges to restrict the schedules to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// The times in the response are wall-clock times in the venue's own time zone, which the
    /// schedule reports as an IANA identifier.
    /// </remarks>
    Task<IReadOnlyList<TradingSchedule>> GetTradingScheduleAsync(
        string assetClass,
        string symbol,
        string? exchange = null,
        string? exchangeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the currency pairs tradable against a currency.</summary>
    /// <param name="currency">The base currency.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<CurrencyPair>>> GetCurrencyPairsAsync(
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the exchange rate between two currencies.</summary>
    /// <param name="target">The target currency.</param>
    /// <param name="source">The source currency.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<ExchangeRate> GetExchangeRateAsync(
        string target,
        string source,
        CancellationToken cancellationToken = default);
}
