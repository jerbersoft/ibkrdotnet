using NodaTime;

namespace IbkrDotNet.Trading.Authentication;

/// <summary>
/// The session token obtained from <c>/tickle</c>, shared between the session manager that refreshes
/// it and the handler that attaches it to outgoing requests.
/// </summary>
/// <remarks>
/// IBKR requires the token be sent back as a cookie in the form <c>api={sessionToken}</c> when
/// authenticating with either OAuth flow. Register this as a singleton: one brokerage session exists
/// per username at a time, and every request in the process shares it.
/// </remarks>
public sealed class IbkrSessionState
{
    private readonly Lock _sync = new();
    private string? _sessionToken;
    private Instant? _obtainedAt;

    /// <summary>The current session token, or <see langword="null"/> when none has been obtained.</summary>
    public string? SessionToken
    {
        get
        {
            lock (_sync)
            {
                return _sessionToken;
            }
        }
    }

    /// <summary>When the current token was obtained.</summary>
    public Instant? ObtainedAt
    {
        get
        {
            lock (_sync)
            {
                return _obtainedAt;
            }
        }
    }

    /// <summary>Records a newly obtained session token.</summary>
    /// <param name="sessionToken">The token returned by <c>/tickle</c>.</param>
    /// <param name="obtainedAt">When it was obtained.</param>
    public void Set(string sessionToken, Instant obtainedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        lock (_sync)
        {
            _sessionToken = sessionToken;
            _obtainedAt = obtainedAt;
        }
    }

    /// <summary>
    /// Discards the current token.
    /// </summary>
    /// <remarks>
    /// IBKR requires client-side cookies be discarded when a session ends, whether through
    /// <c>/logout</c> or by lapsing.
    /// </remarks>
    public void Clear()
    {
        lock (_sync)
        {
            _sessionToken = null;
            _obtainedAt = null;
        }
    }
}
