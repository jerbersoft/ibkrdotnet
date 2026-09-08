using IbkrDotNet.Trading.Models.Accounts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Trading Accounts endpoints: which accounts may be traded, and their balances, margin and
/// profit and loss.
/// </summary>
public interface IAccountsClient
{
    /// <summary>
    /// Lists the accounts the authenticated username may trade.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// These are the accounts that may be <em>traded</em>. For the accounts whose positions and
    /// balances may be <em>viewed</em>, use <c>GET /portfolio/accounts</c>; the two
    /// sets are not always the same.
    /// </remarks>
    Task<TradableAccounts> GetTradableAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns an overview of the account's balances and margin.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountSummary> GetSummaryAsync(AccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>Returns balance details by account segment.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountSegmentSummary> GetBalanceSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns margin usage by account segment.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountSegmentSummary> GetMarginSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns market value by currency.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountSegmentSummary> GetMarketValueSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns available funds by account segment.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountSegmentSummary> GetAvailableFundsSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns profit and loss for the account's partitions.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>Limited by IBKR to one request every five seconds.</remarks>
    Task<AccountPnl> GetProfitAndLossAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches dynamic accounts by pattern.</summary>
    /// <param name="searchPattern">The pattern to match.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<DynamicAccountSearchResult> SearchDynamicAccountsAsync(
        string searchPattern,
        CancellationToken cancellationToken = default);

    /// <summary>Lists an account's signatures and owners.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AccountOwners> GetOwnersAsync(AccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the account subsequent requests operate against.
    /// </summary>
    /// <param name="accountId">The account to select.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SetAccountResponse> SwitchAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the active dynamic account.</summary>
    /// <param name="accountId">The account to activate.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SetAccountResponse> SetDynamicAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);
}
