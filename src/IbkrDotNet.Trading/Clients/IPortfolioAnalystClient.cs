using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Primitives;
using NodaTime;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// PortfolioAnalyst: account performance, portfolio allocation and transaction history.
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint in this group is limited to one request per fifteen minutes, the tightest limit on
/// the API apart from the scanner's parameter list. The client paces itself, so a second call simply
/// waits; what it cannot do is make the wait shorter. Cache what you read, and prefer
/// <see cref="GetAllPeriodsPerformanceAsync"/> over several <see cref="GetPerformanceAsync"/> calls
/// -- it returns every period at once and spends one of the same windows.
/// </para>
/// <para>
/// These are reads, in spite of being <c>POST</c>: the parameters are sent as a JSON body rather
/// than as a query string, and nothing about the account changes. The <c>/pa/allocation</c> page is
/// titled "Create Allocation" in IBKR's own reference and creates nothing.
/// </para>
/// <para>
/// The group sits outside <c>/iserver</c>, so it needs only the authenticated session and not a
/// brokerage session -- these endpoints keep answering while Trader Workstation holds the brokerage
/// session for the same username.
/// </para>
/// </remarks>
public interface IPortfolioAnalystClient
{
    /// <summary>
    /// Returns the mark-to-market performance of one or more accounts over a single period.
    /// </summary>
    /// <param name="accountIds">The accounts to report on. Several are consolidated into one result.</param>
    /// <param name="period">The window to report over.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <remarks>
    /// One request per fifteen minutes. <see cref="GetAllPeriodsPerformanceAsync"/> covers every
    /// period for the same cost, so reach for this one only when the extra data would be wasted.
    /// </remarks>
    Task<AccountPerformance> GetPerformanceAsync(
        IEnumerable<AccountId> accountIds,
        PerformancePeriod period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the performance of one or more accounts across every period at once.
    /// </summary>
    /// <param name="accountIds">The accounts to report on. Several are consolidated into one result.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <remarks>
    /// One request per fifteen minutes.
    /// </remarks>
    Task<PerformanceAllPeriods> GetAllPeriodsPerformanceAsync(
        IEnumerable<AccountId> accountIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an account's transactions in the given contracts.
    /// </summary>
    /// <param name="accountIds">The accounts to report on.</param>
    /// <param name="conIds">
    /// The contracts to report on. IBKR's endpoint guide states that only one is supported at a
    /// time; the field is nonetheless an array on the wire, and is passed through as one.
    /// </param>
    /// <param name="currency">
    /// The currency to price the amounts in. Defaults to <c>USD</c>, which the client sends
    /// explicitly: IBKR documents the field as optional and then rejects a request without it.
    /// </param>
    /// <param name="days">
    /// How many days back to report. IBKR defaults to 90. The window is inclusive of both ends, so a
    /// request for 365 days is answered with a <see cref="TransactionHistory.DayCount"/> of 366.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <remarks>
    /// One request per fifteen minutes. Returns more than trades: dividends, payments in lieu and
    /// transfers all appear.
    /// </remarks>
    Task<TransactionHistory> GetTransactionsAsync(
        IEnumerable<AccountId> accountIds,
        IEnumerable<ConId> conIds,
        string? currency = null,
        int? days = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns how a portfolio is spread across a category.
    /// </summary>
    /// <param name="accountIds">The accounts to report on. Several are aggregated into one result.</param>
    /// <param name="type">
    /// The category to break the portfolio down by, or <see cref="PortfolioAllocationType.All"/> for
    /// every category in one response.
    /// </param>
    /// <param name="currency">
    /// The currency to value the portfolio in. Genuinely optional here, unlike on
    /// <see cref="GetTransactionsAsync"/>: a live gateway answered a request without it and filled
    /// in <c>USD</c> itself, so it is omitted rather than guessed at. Current-day figures are only
    /// available when every account's base currency matches; otherwise IBKR answers for the previous
    /// business day instead of failing.
    /// </param>
    /// <param name="asOfDate">
    /// The day to report on, which must be in the past. Omit for the current day.
    /// </param>
    /// <param name="model">The model portfolio to calculate against, where one applies.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <remarks>
    /// One request per fifteen minutes, so ask for
    /// <see cref="PortfolioAllocationType.All"/> rather than spending four windows on four
    /// categories.
    /// </remarks>
    Task<PortfolioAllocation> GetAllocationAsync(
        IEnumerable<AccountId> accountIds,
        PortfolioAllocationType type,
        string? currency = null,
        LocalDate? asOfDate = null,
        string? model = null,
        CancellationToken cancellationToken = default);
}
