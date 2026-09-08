using IbkrDotNet.Trading.Models.Session;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Trading Session endpoints, covering the lifecycle every other endpoint depends on.
/// </summary>
/// <remarks>
/// IBKR sessions are two-tiered. An outer read-only session is required for any request at all but
/// by itself only reaches non-<c>/iserver</c> endpoints; a brokerage session, established on top of
/// it, is what permits trading, market data and everything else behind <c>/iserver</c>.
/// </remarks>
public interface ISessionClient
{
    /// <summary>
    /// Reports the current brokerage session status.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>This is a <c>POST</c> despite reading nothing, which is IBKR's design.</remarks>
    Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pings IBKR to keep the session alive and returns the session token.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// A session times out after several minutes without traffic, so this should be called roughly
    /// every 60 seconds. It is limited to one request per second.
    /// </remarks>
    Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the brokerage session, which must succeed before any <c>/iserver</c> endpoint
    /// will answer.
    /// </summary>
    /// <param name="compete">
    /// Whether to take the brokerage session over from another platform currently holding it. A
    /// username may hold only one at a time, so <see langword="true"/> displaces an existing Trader
    /// Workstation or Client Portal session rather than failing.
    /// </param>
    /// <param name="publish">
    /// Whether to publish the session token at initialization. IBKR documents <see langword="true"/>
    /// as the preferred value.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<BrokerageSessionStatus> InitializeAsync(
        bool compete = true,
        bool publish = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the outer read-only Web API session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>Limited to one request per minute.</remarks>
    Task<SsoValidationResponse> ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the Web API session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default);
}
