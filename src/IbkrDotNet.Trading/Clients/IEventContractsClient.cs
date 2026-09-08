using IbkrDotNet.Trading.Models.EventContracts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The event contract endpoints: ForecastEx markets, the contracts written on them, and their terms.
/// </summary>
/// <remarks>
/// <para>
/// Event contracts settle on a published fact rather than on a price -- an election result, an
/// inflation print, a temperature -- and pay a fixed amount if the answer is yes. Each question is
/// listed as a pair of contracts, one for each side, and each has its own conid.
/// </para>
/// <para>
/// Discovery runs downwards and the identifiers are not interchangeable.
/// <see cref="GetCategoryTreeAsync"/> is the only place a market's conid can be found; that conid
/// goes to <see cref="GetMarketAsync"/>, which lists the individual contracts; and a contract's
/// conid goes to the other three. Passing a market conid where a contract conid belongs answers
/// <c>404</c>, as does passing a product conid where a market conid belongs.
/// </para>
/// <para>
/// Every endpoint here is a read, and none of them needs a brokerage session beyond the one the
/// gateway already holds.
/// </para>
/// </remarks>
public interface IEventContractsClient
{
    /// <summary>
    /// Gets the whole category tree, and with it every market's contract identifier.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The categories, keyed by identifier.</returns>
    /// <remarks>
    /// One response covering the entire product set -- a live gateway returns just over three
    /// hundred categories and nine hundred markets, around 150 KB. There is no way to ask for a
    /// subtree, so fetch it once and hold it rather than calling it per lookup.
    /// </remarks>
    Task<EventContractCategoryTree> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a market and the contracts written on it.
    /// </summary>
    /// <param name="underlyingConId">
    /// The market's identifier, from <see cref="EventContractMarketSummary.ConId"/> in the category
    /// tree. Not the product identifier beside it, which answers <c>404</c>.
    /// </param>
    /// <param name="exchange">The exchange to resolve against. IBKR determines it internally when omitted.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The market, with both sides of each strike.</returns>
    Task<EventContractMarket> GetMarketAsync(
        ConId underlyingConId,
        string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full detail of one contract, including both sides of its question.
    /// </summary>
    /// <param name="conId">A single side's contract identifier, from <see cref="EventContractSummary.ConId"/>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The contract's detail.</returns>
    /// <remarks>
    /// Either side's conid may be passed; the response carries both, and reports which one was
    /// asked for in <see cref="EventContractDetails.Side"/>.
    /// </remarks>
    Task<EventContractDetails> GetDetailsAsync(ConId conId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets how a contract settles: the source agency, the thresholds, and the settlement times.
    /// </summary>
    /// <param name="conId">A single side's contract identifier.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The contract's terms.</returns>
    Task<EventContractRules> GetRulesAsync(ConId conId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a contract's weekly trading pattern.
    /// </summary>
    /// <param name="conId">A single side's contract identifier.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The days of the week the contract trades, and the blocks within each.</returns>
    /// <remarks>
    /// A recurring weekly pattern in the exchange's own zone, not a list of dated sessions, and it
    /// says nothing about holidays. For a dated calendar use the trading schedule on
    /// <see cref="IContractsClient"/> instead.
    /// </remarks>
    Task<EventContractSchedule> GetScheduleAsync(ConId conId, CancellationToken cancellationToken = default);
}
