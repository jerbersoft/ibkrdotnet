using IbkrDotNet.Trading.Models.Portfolio;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Trading Portfolio endpoints: accounts, positions, balances and allocations.
/// </summary>
/// <remarks>
/// <see cref="GetAccountsAsync"/> must be called before any other endpoint in this group for a given
/// account. IBKR does not error when it is skipped; it returns empty or stale data instead, which is
/// considerably harder to diagnose.
/// </remarks>
public interface IPortfolioClient
{
    /// <summary>
    /// Lists the accounts whose positions and balances the username may view.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Call this before any other <c>/portfolio</c> endpoint for those accounts. These are viewable
    /// accounts; for the accounts that may be <em>traded</em>, use
    /// <see cref="IAccountsClient.GetTradableAccountsAsync"/>. Limited to one request every five
    /// seconds.
    /// </remarks>
    Task<IReadOnlyList<PortfolioAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists up to 100 subaccounts in a multi-level account structure.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Use <see cref="GetSubaccountsPageAsync"/> when there are more than 100. Limited to one
    /// request every five seconds.
    /// </remarks>
    Task<IReadOnlyList<PortfolioAccount>> GetSubaccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a page of subaccounts, for structures with more than 100.</summary>
    /// <param name="page">The zero-based page number.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SubaccountsPage> GetSubaccountsPageAsync(int page = 0, CancellationToken cancellationToken = default);

    /// <summary>Returns an account's attributes.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<PortfolioAccount> GetAccountMetadataAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an account's balance summary, keyed by summary field.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyDictionary<string, PortfolioSummaryValue>> GetSummaryAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an account's cash balances, keyed by currency.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>The <c>BASE</c> key holds the account's base-currency totals.</remarks>
    Task<IReadOnlyDictionary<string, LedgerEntry>> GetLedgerAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an account's allocations by asset class, sector and industry group.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<AssetAllocation> GetAllocationAsync(AccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>Returns a page of an account's positions.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="pageId">The zero-based page. Each page holds up to 100 positions.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<Position>> GetPositionsAsync(
        AccountId accountId,
        int pageId = 0,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an account's position in one instrument.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="conId">The instrument.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<Position>> GetPositionAsync(
        AccountId accountId,
        ConId conId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every account's position in one instrument, keyed by account identifier.
    /// </summary>
    /// <param name="conId">The instrument.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsByInstrumentAsync(
        ConId conId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an account's combination positions.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<ComboPosition>> GetComboPositionsAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an account's positions from IBKR's newer, uncached positions endpoint.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// A leaner view than <see cref="GetPositionsAsync"/>, served without the position cache, so it
    /// needs no invalidation.
    /// </remarks>
    Task<IReadOnlyList<UncachedPosition>> GetUncachedPositionsAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached positions for an account so the next read is fresh.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// <see cref="GetPositionsAsync"/> is served from a cache that IBKR does not refresh on every
    /// fill, so call this after trading if the numbers must be current.
    /// </remarks>
    Task<InvalidatePositionCacheResponse> InvalidatePositionCacheAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);
}
