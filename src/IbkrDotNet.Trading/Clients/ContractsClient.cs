using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Contracts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IContractsClient" />
public sealed class ContractsClient(IIbkrApiClient apiClient) : IContractsClient
{
    /// <summary>
    /// The most contract identifiers IBKR accepts in one <c>/trsrv/secdef</c> request.
    /// </summary>
    public const int MaxDefinitionsPerRequest = 200;

    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<IReadOnlyList<ContractSearchResult>> SearchAsync(
        string symbol,
        string? securityType = null,
        bool? searchByName = null,
        bool? more = null,
        bool? fund = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return _apiClient.SendAsync<IReadOnlyList<ContractSearchResult>>(
            IbkrRequest.Get("/v1/api/iserver/secdef/search")
                .WithQuery("symbol", symbol)
                .WithQuery("secType", securityType)
                .WithQuery("name", searchByName)
                .WithQuery("more", more)
                .WithQuery("fund", fund),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<InstrumentAttributes>> GetAttributesAsync(
        ConId conId,
        string? securityType = null,
        string? month = null,
        string? exchange = null,
        string? strike = null,
        string? right = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<InstrumentAttributes>>(
            IbkrRequest.Get("/v1/api/iserver/secdef/info")
                .WithQuery("conid", conId.Value)
                .WithQuery("sectype", securityType)
                .WithQuery("month", month)
                .WithQuery("exchange", exchange)
                .WithQuery("strike", strike)
                .WithQuery("right", right),
            cancellationToken);

    /// <inheritdoc />
    public Task<OptionStrikes> GetStrikesAsync(
        ConId conId,
        string securityType,
        string month,
        string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(month);
        return _apiClient.SendAsync<OptionStrikes>(
            IbkrRequest.Get("/v1/api/iserver/secdef/strikes")
                .WithQuery("conid", conId.Value)
                .WithQuery("sectype", securityType)
                .WithQuery("month", month)
                .WithQuery("exchange", exchange),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<BondFilters> GetBondFiltersAsync(
        string symbol,
        string issuerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        return _apiClient.SendAsync<BondFilters>(
            IbkrRequest.Get("/v1/api/iserver/secdef/bond-filters")
                .WithQuery("symbol", symbol)
                .WithQuery("issuerId", issuerId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<InstrumentInfo> GetInfoAsync(ConId conId, CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<InstrumentInfo>(
            IbkrRequest.Get($"/v1/api/iserver/contract/{IbkrRequest.PathSegment(conId.Value)}/info"),
            cancellationToken);

    /// <inheritdoc />
    public Task<InstrumentInfo> GetInfoAndRulesAsync(ConId conId, CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<InstrumentInfo>(
            IbkrRequest.Get($"/v1/api/iserver/contract/{IbkrRequest.PathSegment(conId.Value)}/info-and-rules"),
            cancellationToken);

    /// <inheritdoc />
    public Task<ContractRules> GetRulesAsync(
        ConId conId,
        bool isBuy = true,
        bool modifyOrder = false,
        long? orderId = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<ContractRules>(
            IbkrRequest.Post("/v1/api/iserver/contract/rules")
                .WithJsonBody(new ContractRulesRequest(conId.Value, isBuy, modifyOrder, orderId)),
            cancellationToken);

    /// <inheritdoc />
    public Task<InstrumentAlgorithms> GetAlgorithmsAsync(
        ConId conId,
        IEnumerable<string>? algorithmIds = null,
        bool includeDescriptions = false,
        bool includeParameters = false,
        CancellationToken cancellationToken = default)
    {
        var request = IbkrRequest
            .Get($"/v1/api/iserver/contract/{IbkrRequest.PathSegment(conId.Value)}/algos")
            .WithQuery("addDescription", includeDescriptions ? "1" : "0")
            .WithQuery("addParams", includeParameters ? "1" : "0");

        // Unlike every other list parameter in the API, this one is semicolon delimited.
        if (algorithmIds is not null)
        {
            var joined = string.Join(';', algorithmIds);
            if (joined.Length > 0)
            {
                request.WithQuery("algos", joined);
            }
        }

        return _apiClient.SendAsync<InstrumentAlgorithms>(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<InstrumentDefinitions> GetDefinitionsAsync(
        IEnumerable<ConId> conIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conIds);
        var ids = conIds.Select(c => c.ToString()).ToList();

        if (ids.Count > MaxDefinitionsPerRequest)
        {
            // Refusing is better than truncating: IBKR would return a partial result and the caller
            // would have no way to tell which instruments were dropped.
            throw new ArgumentException(
                $"IBKR accepts at most {MaxDefinitionsPerRequest} contract identifiers per " +
                $"/trsrv/secdef request; {ids.Count} were supplied. Split the request.",
                nameof(conIds));
        }

        return _apiClient.SendAsync<InstrumentDefinitions>(
            IbkrRequest.Get("/v1/api/trsrv/secdef").WithCommaSeparatedQuery("conids", ids),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<StockListing>>> GetStocksAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyDictionary<string, IReadOnlyList<StockListing>>>(
            IbkrRequest.Get("/v1/api/trsrv/stocks").WithCommaSeparatedQuery("symbols", symbols),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<FutureContract>>> GetFuturesAsync(
        IEnumerable<string> symbols,
        string? exchange = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyDictionary<string, IReadOnlyList<FutureContract>>>(
            IbkrRequest.Get("/v1/api/trsrv/futures")
                .WithCommaSeparatedQuery("symbols", symbols)
                .WithQuery("exchange", exchange),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ExchangeListing>> GetListingsByExchangeAsync(
        string exchange,
        string? assetClass = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        return _apiClient.SendAsync<IReadOnlyList<ExchangeListing>>(
            IbkrRequest.Get("/v1/api/trsrv/all-conids")
                .WithQuery("exchange", exchange)
                .WithQuery("assetClass", assetClass),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TradingSchedule>> GetTradingScheduleAsync(
        string assetClass,
        string symbol,
        string? exchange = null,
        string? exchangeFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return _apiClient.SendAsync<IReadOnlyList<TradingSchedule>>(
            IbkrRequest.Get("/v1/api/trsrv/secdef/schedule")
                .WithQuery("assetClass", assetClass)
                .WithQuery("symbol", symbol)
                .WithQuery("exchange", exchange)
                .WithQuery("exchangeFilter", exchangeFilter),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<CurrencyPair>>> GetCurrencyPairsAsync(
        string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        return _apiClient.SendAsync<IReadOnlyDictionary<string, IReadOnlyList<CurrencyPair>>>(
            IbkrRequest.Get("/v1/api/iserver/currency/pairs").WithQuery("currency", currency),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExchangeRate> GetExchangeRateAsync(
        string target,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return _apiClient.SendAsync<ExchangeRate>(
            IbkrRequest.Get("/v1/api/iserver/exchangerate")
                .WithQuery("target", target)
                .WithQuery("source", source),
            cancellationToken);
    }
}
