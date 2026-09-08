using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Accounts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IAccountsClient" />
public sealed class AccountsClient(IIbkrApiClient apiClient) : IAccountsClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<TradableAccounts> GetTradableAccountsAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<TradableAccounts>(IbkrRequest.Get("/v1/api/iserver/accounts"), cancellationToken);

    /// <inheritdoc />
    public Task<AccountSummary> GetSummaryAsync(AccountId accountId, CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AccountSummary>(
            IbkrRequest.Get($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/summary"),
            cancellationToken);

    /// <inheritdoc />
    public Task<AccountSegmentSummary> GetBalanceSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        GetSegmentSummaryAsync(accountId, "balances", cancellationToken);

    /// <inheritdoc />
    public Task<AccountSegmentSummary> GetMarginSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        GetSegmentSummaryAsync(accountId, "margins", cancellationToken);

    /// <inheritdoc />
    public Task<AccountSegmentSummary> GetMarketValueSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        GetSegmentSummaryAsync(accountId, "market_value", cancellationToken);

    /// <inheritdoc />
    public Task<AccountSegmentSummary> GetAvailableFundsSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        GetSegmentSummaryAsync(accountId, "available_funds", cancellationToken);

    /// <inheritdoc />
    public Task<AccountPnl> GetProfitAndLossAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AccountPnl>(
            IbkrRequest.Get("/v1/api/iserver/account/pnl/partitioned"),
            cancellationToken);

    /// <inheritdoc />
    public Task<DynamicAccountSearchResult> SearchDynamicAccountsAsync(
        string searchPattern,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        return _apiClient.SendAsync<DynamicAccountSearchResult>(
            IbkrRequest.Get($"/v1/api/iserver/account/search/{IbkrRequest.PathSegment(searchPattern)}"),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<AccountOwners> GetOwnersAsync(AccountId accountId, CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AccountOwners>(
            IbkrRequest.Get($"/v1/api/acesws/{IbkrRequest.PathSegment(accountId.Value)}/signatures-and-owners"),
            cancellationToken);

    /// <inheritdoc />
    public Task<SetAccountResponse> SwitchAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<SetAccountResponse>(
            IbkrRequest.Post("/v1/api/iserver/account").WithJsonBody(new { acctId = accountId.Value }),
            cancellationToken);

    /// <inheritdoc />
    public Task<SetAccountResponse> SetDynamicAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<SetAccountResponse>(
            IbkrRequest.Post("/v1/api/iserver/dynaccount").WithJsonBody(new { acctId = accountId.Value }),
            cancellationToken);

    private Task<AccountSegmentSummary> GetSegmentSummaryAsync(
        AccountId accountId,
        string segment,
        CancellationToken cancellationToken) =>
        _apiClient.SendAsync<AccountSegmentSummary>(
            IbkrRequest.Get(
                $"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/summary/{segment}"),
            cancellationToken);
}
