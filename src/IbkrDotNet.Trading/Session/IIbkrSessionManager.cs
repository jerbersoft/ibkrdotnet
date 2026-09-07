using IbkrDotNet.Trading.Models.Session;

namespace IbkrDotNet.Trading.Session;

/// <summary>
/// Establishes and maintains the brokerage session that <c>/iserver</c> endpoints require.
/// </summary>
/// <remarks>
/// Wraps <see cref="Clients.ISessionClient"/> with the lifecycle around it: capturing the session
/// token from <c>/tickle</c> so it can be sent back as the <c>api=</c> cookie, initializing the
/// brokerage session when it is not established, and discarding the token on logout.
/// </remarks>
public interface IIbkrSessionManager
{
    /// <summary>
    /// Ensures a brokerage session is established, initializing one if it is not.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The resulting session status.</returns>
    /// <remarks>
    /// Safe to call before any request. Concurrent callers share a single initialization rather than
    /// each starting one.
    /// </remarks>
    Task<BrokerageSessionStatus> EnsureBrokerageSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pings IBKR to keep the session alive, recording the session token it returns.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <remarks>
    /// Call roughly every 60 seconds; sessions time out after several idle minutes. Limited by IBKR
    /// to one request per second.
    /// </remarks>
    Task<TickleResponse> KeepAliveAsync(CancellationToken cancellationToken = default);

    /// <summary>Reports the current brokerage session status without changing it.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the Web API session and discards the client-side session token.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default);
}
