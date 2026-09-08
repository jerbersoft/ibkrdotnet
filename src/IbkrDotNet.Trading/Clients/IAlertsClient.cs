using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Alerts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Alerts endpoints, covering the price and time alerts saved against an account.
/// </summary>
/// <remarks>
/// <para>
/// These are the same alerts Trader Workstation and Client Portal show, and activating or deleting
/// one through this client changes what the user sees there. An alert only observes the market; it
/// never places an order.
/// </para>
/// <para>
/// The group is read-and-destroy rather than a full lifecycle: IBKR publishes no endpoint that
/// creates an alert, so <see cref="DeleteAsync"/> is one-way. Alerts have to be created in Trader
/// Workstation or Client Portal, and this library cannot put back what it removes.
/// </para>
/// <para>
/// Every alert also carries a time in force and an order status, and its identifier is called
/// <c>order_id</c>, because TWS builds an alert out of the order system. The identifier is typed as
/// an <see cref="OrderId"/> here for that reason; IBKR's own paths call the same value
/// <c>alertId</c>.
/// </para>
/// </remarks>
public interface IAlertsClient
{
    /// <summary>
    /// Lists the alerts saved against an account.
    /// </summary>
    /// <param name="accountId">The account whose alerts to list.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// An account with no alerts answers with an empty array rather than an error. The listing does
    /// not include the mobile trading assistant alert, which
    /// <see cref="GetMobileTradingAssistantAsync"/> reads instead, and carries none of an alert's
    /// conditions -- read one with <see cref="GetDetailsAsync"/> to see what arms it.
    /// </remarks>
    Task<IReadOnlyList<AlertSummary>> GetAllAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one alert in full, including its conditions.
    /// </summary>
    /// <param name="alertId">The alert to read, from <see cref="AlertSummary.Id"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IbkrApiException">
    /// The alert does not exist. IBKR reports that as <c>200 OK</c> carrying
    /// <c>{"error": "Alert with order ID=... not found."}</c> rather than as a <c>404</c>, so this
    /// is raised from the body rather than from the status code.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Not scoped to an account: the path is <c>/iserver/account/alert/{alertId}</c>, with no
    /// account segment, and the alert names its own account back.
    /// </para>
    /// <para>
    /// The mobile trading assistant alert cannot be read here even though it reports an identifier
    /// of the same shape -- passing it answers "not found", because that identifier was never one.
    /// See <see cref="GetMobileTradingAssistantAsync"/>.
    /// </para>
    /// </remarks>
    Task<AlertDetails> GetDetailsAsync(OrderId alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the username's mobile trading assistant alert.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// <para>
    /// Every username has exactly one, it cannot be created or deleted, and it is the only alert
    /// that fills in <see cref="AlertDetails.MtaCurrency"/> and
    /// <see cref="AlertDetails.MtaDefaults"/>.
    /// </para>
    /// <para>
    /// Do not keep its <see cref="AlertDetails.Id"/>. A live gateway returns a different one from
    /// every call -- the value increments per read, drawn from the account's order sequence -- and
    /// no endpoint accepts it. <see cref="AlertDetails.ToolId"/> is the identifier that stays put.
    /// </para>
    /// </remarks>
    Task<AlertDetails> GetMobileTradingAssistantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arms or disarms an existing alert, without deleting it.
    /// </summary>
    /// <param name="accountId">The account the alert belongs to.</param>
    /// <param name="alertId">The alert to activate or deactivate.</param>
    /// <param name="active">Whether the alert should be armed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// IBKR acknowledges the request rather than the outcome -- the response says "Request was
    /// submitted" -- so read the alert back with <see cref="GetDetailsAsync"/> to confirm it took.
    /// </remarks>
    Task<AlertActionResult> SetActiveAsync(
        AccountId accountId,
        OrderId alertId,
        bool active,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes an alert.
    /// </summary>
    /// <param name="accountId">The account the alert belongs to.</param>
    /// <param name="alertId">The alert to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// There is no endpoint that creates an alert, so this cannot be undone through the API: a
    /// deleted alert has to be recreated by hand in Trader Workstation or Client Portal. To stop an
    /// alert firing without losing it, deactivate it with <see cref="SetActiveAsync"/> instead.
    /// Deleting the mobile trading assistant alert resets it to its defaults rather than removing it.
    /// </remarks>
    Task<AlertActionResult> DeleteAsync(
        AccountId accountId,
        OrderId alertId,
        CancellationToken cancellationToken = default);
}
