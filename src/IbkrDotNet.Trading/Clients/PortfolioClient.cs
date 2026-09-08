using System.Globalization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Portfolio;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IPortfolioClient" />
public sealed class PortfolioClient(IIbkrApiClient apiClient) : IPortfolioClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<IReadOnlyList<PortfolioAccount>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<PortfolioAccount>>(
            IbkrRequest.Get("/v1/api/portfolio/accounts"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PortfolioAccount>> GetSubaccountsAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<PortfolioAccount>>(
            IbkrRequest.Get("/v1/api/portfolio/subaccounts"),
            cancellationToken);

    /// <inheritdoc />
    public Task<SubaccountsPage> GetSubaccountsPageAsync(
        int page = 0,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<SubaccountsPage>(
            IbkrRequest.Get("/v1/api/portfolio/subaccounts2").WithQuery("page", page.ToString(CultureInfo.InvariantCulture)),
            cancellationToken);

    /// <inheritdoc />
    public Task<PortfolioAccount> GetAccountMetadataAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<PortfolioAccount>(
            IbkrRequest.Get($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/meta"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PortfolioSummaryValue>> GetSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyDictionary<string, PortfolioSummaryValue>>(
            IbkrRequest.Get($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/summary"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, LedgerEntry>> GetLedgerAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyDictionary<string, LedgerEntry>>(
            IbkrRequest.Get($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/ledger"),
            cancellationToken);

    /// <inheritdoc />
    public Task<AssetAllocation> GetAllocationAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AssetAllocation>(
            IbkrRequest.Get($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/allocation"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Position>> GetPositionsAsync(
        AccountId accountId,
        int pageId = 0,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<Position>>(
            IbkrRequest.Get(
                $"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/positions/{IbkrRequest.PathSegment(pageId)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Position>> GetPositionAsync(
        AccountId accountId,
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<Position>>(
            IbkrRequest.Get(
                $"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/position/{IbkrRequest.PathSegment(conId.Value)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsByInstrumentAsync(
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(
            IbkrRequest.Get($"/v1/api/portfolio/positions/{IbkrRequest.PathSegment(conId.Value)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ComboPosition>> GetComboPositionsAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<ComboPosition>>(
            IbkrRequest.Get($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/combo/positions"),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<UncachedPosition>> GetUncachedPositionsAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<UncachedPosition>>(
            IbkrRequest.Get($"/v1/api/portfolio2/{IbkrRequest.PathSegment(accountId.Value)}/positions"),
            cancellationToken);

    /// <inheritdoc />
    public Task<InvalidatePositionCacheResponse> InvalidatePositionCacheAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<InvalidatePositionCacheResponse>(
            IbkrRequest.Post($"/v1/api/portfolio/{IbkrRequest.PathSegment(accountId.Value)}/positions/invalidate"),
            cancellationToken);
}
