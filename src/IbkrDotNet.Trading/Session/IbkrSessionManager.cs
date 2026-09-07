using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.Session;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace IbkrDotNet.Trading.Session;

/// <inheritdoc cref="IIbkrSessionManager" />
public sealed class IbkrSessionManager : IIbkrSessionManager, IDisposable
{
    private readonly ISessionClient _sessionClient;
    private readonly IbkrSessionState _sessionState;
    private readonly IClock _clock;
    private readonly ILogger<IbkrSessionManager> _logger;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _disposed;

    /// <summary>Creates the session manager.</summary>
    /// <param name="sessionClient">The session endpoint client.</param>
    /// <param name="sessionState">The shared session token.</param>
    /// <param name="clock">The clock used to timestamp the session token.</param>
    /// <param name="logger">The logger.</param>
    public IbkrSessionManager(
        ISessionClient sessionClient,
        IbkrSessionState sessionState,
        IClock clock,
        ILogger<IbkrSessionManager> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionClient);
        ArgumentNullException.ThrowIfNull(sessionState);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionClient = sessionClient;
        _sessionState = sessionState;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BrokerageSessionStatus> EnsureBrokerageSessionAsync(
        CancellationToken cancellationToken = default)
    {
        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Tickle first: it both proves the outer session is alive and yields the token that
            // OAuth callers must echo back as a cookie on the /iserver call that follows.
            var tickle = await KeepAliveCoreAsync(cancellationToken).ConfigureAwait(false);

            if (tickle.AuthenticationStatus is { IsReadyToTrade: true } ready)
            {
                return ready;
            }

            SessionLog.InitializingBrokerageSession(_logger);
            var status = await _sessionClient
                .InitializeAsync(compete: true, publish: true, cancellationToken)
                .ConfigureAwait(false);

            if (!status.IsReadyToTrade)
            {
                SessionLog.BrokerageSessionNotEstablished(
                    _logger,
                    status.Connected,
                    status.Authenticated,
                    status.Established,
                    status.Competing,
                    status.Message ?? status.Fail ?? string.Empty);
            }

            return status;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<TickleResponse> KeepAliveAsync(CancellationToken cancellationToken = default) =>
        KeepAliveCoreAsync(cancellationToken);

    /// <inheritdoc />
    public Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _sessionClient.GetStatusAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _sessionClient.LogoutAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // IBKR requires client-side cookies be discarded once a session ends, whether or not
            // the logout call itself succeeded.
            _sessionState.Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _initializationGate.Dispose();
        }
    }

    private async Task<TickleResponse> KeepAliveCoreAsync(CancellationToken cancellationToken)
    {
        var tickle = await _sessionClient.TickleAsync(cancellationToken).ConfigureAwait(false);

        if (tickle.Session is { Length: > 0 } token)
        {
            _sessionState.Set(token, _clock.GetCurrentInstant());
        }

        if (tickle.AuthenticationStatus is { Competing: true })
        {
            SessionLog.CompetingSessionDetected(_logger);
        }

        return tickle;
    }
}

internal static partial class SessionLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Initializing the IBKR brokerage session.")]
    public static partial void InitializingBrokerageSession(ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "The IBKR brokerage session is not ready to trade " +
                  "(connected={Connected}, authenticated={Authenticated}, established={Established}, " +
                  "competing={Competing}). {Message}")]
    public static partial void BrokerageSessionNotEstablished(
        ILogger logger,
        bool connected,
        bool authenticated,
        bool established,
        bool competing,
        string message);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Warning,
        Message = "Another platform is competing for this IBKR username's brokerage session. " +
                  "A username may hold only one at a time, so this session may be displaced.")]
    public static partial void CompetingSessionDetected(ILogger logger);
}
