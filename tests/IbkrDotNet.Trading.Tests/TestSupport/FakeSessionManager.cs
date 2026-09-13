using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Session;
using NodaTime;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// A session manager that answers without a network, recording the session token the way a real
/// tickle would so the transport can present it as the cookie.
/// </summary>
public sealed class FakeSessionManager(IbkrSessionState state, IClock clock) : IIbkrSessionManager
{
    /// <summary>The session token from IBKR's own WebSocket walkthrough.</summary>
    public const string DocumentedToken = "d21b8cf5ebc8ea01c6ce37c8125ec83f";

    public bool Ready { get; set; } = true;

    public string Token { get; set; } = DocumentedToken;

    public int EnsureCalls { get; private set; }

    public Task<BrokerageSessionStatus> EnsureBrokerageSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureCalls++;
        RecordToken();
        return Task.FromResult(Status());
    }

    public Task<TickleResponse> KeepAliveAsync(CancellationToken cancellationToken = default)
    {
        RecordToken();
        return Task.FromResult(new TickleResponse { Session = Token });
    }

    public Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Status());

    public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
    {
        state.Clear();
        return Task.FromResult(new LogoutResponse());
    }

    private void RecordToken()
    {
        if (Token is { Length: > 0 })
        {
            state.Set(Token, clock.GetCurrentInstant());
        }
    }

    private BrokerageSessionStatus Status() => new()
    {
        Connected = Ready,
        Authenticated = Ready,
        Established = Ready,
        Message = Ready ? null : "not logged in",
    };
}

/// <summary>An authenticator that presents whatever streaming credential the test gives it.</summary>
public sealed class FakeAuthenticator(StreamingCredential? credential = null) : IIbkrAuthenticator
{
    public string Scheme => "Fake";

    public bool RequiresSessionCookie => credential is not null;

    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask<StreamingCredential?> GetStreamingCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(credential);
}
