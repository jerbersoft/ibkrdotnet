using IbkrDotNet.Trading.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>Configures the background brokerage session keep-alive.</summary>
public sealed class BrokerageSessionKeepAliveOptions
{
    /// <summary>
    /// How often to ping IBKR. Defaults to 60 seconds.
    /// </summary>
    /// <remarks>
    /// A session times out after several idle minutes, and <c>/tickle</c> is itself limited to one
    /// request per second, so this belongs comfortably between the two.
    /// </remarks>
    public Duration Interval { get; set; } = Duration.FromSeconds(60);

    /// <summary>
    /// Whether to establish the brokerage session at startup rather than only keeping it alive.
    /// </summary>
    public bool EstablishSessionOnStart { get; set; } = true;
}

/// <summary>
/// Keeps the IBKR brokerage session alive in the background.
/// </summary>
/// <remarks>
/// Failures are logged and retried on the next tick rather than stopping the host: a transient
/// network problem or IBKR's nightly <c>/iserver</c> maintenance window should not take an
/// application down with it.
/// </remarks>
public sealed class BrokerageSessionKeepAlive : BackgroundService
{
    private readonly IIbkrSessionManager _sessionManager;
    private readonly BrokerageSessionKeepAliveOptions _options;
    private readonly ILogger<BrokerageSessionKeepAlive> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="sessionManager">The session manager to drive.</param>
    /// <param name="options">The keep-alive settings.</param>
    /// <param name="logger">The logger.</param>
    public BrokerageSessionKeepAlive(
        IIbkrSessionManager sessionManager,
        IOptions<BrokerageSessionKeepAliveOptions> options,
        ILogger<BrokerageSessionKeepAlive> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionManager = sessionManager;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.EstablishSessionOnStart)
        {
            await SafelyAsync(
                () => _sessionManager.EnsureBrokerageSessionAsync(stoppingToken),
                stoppingToken).ConfigureAwait(false);
        }

        using var timer = new PeriodicTimer(_options.Interval.ToTimeSpan());

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SafelyAsync(
                () => _sessionManager.KeepAliveAsync(stoppingToken),
                stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SafelyAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            KeepAliveLog.KeepAliveFailed(_logger, ex);
        }
    }
}

internal static partial class KeepAliveLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "Keeping the IBKR brokerage session alive failed; retrying on the next interval.")]
    public static partial void KeepAliveFailed(ILogger logger, Exception exception);
}
