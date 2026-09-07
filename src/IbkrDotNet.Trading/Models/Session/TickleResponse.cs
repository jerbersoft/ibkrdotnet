using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Session;

/// <summary>The response from <c>POST /tickle</c>.</summary>
/// <remarks>
/// Besides keeping the session alive, this is where the session token comes from. IBKR requires it
/// be sent back as an <c>api={session}</c> cookie on subsequent requests when authenticating with
/// either OAuth flow.
/// </remarks>
public sealed record TickleResponse
{
    /// <summary>The session token, to be echoed back as the <c>api=</c> cookie.</summary>
    [JsonPropertyName("session")]
    public string? Session { get; init; }

    /// <summary>How long until the current SSO session expires.</summary>
    [JsonPropertyName("ssoExpires")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? SsoExpires { get; init; }

    /// <summary>Internal use.</summary>
    [JsonPropertyName("collission")]
    public bool? Collision { get; init; }

    /// <summary>Internal use.</summary>
    [JsonPropertyName("userId")]
    public long? UserId { get; init; }

    /// <summary>Connection details for the historical market data server.</summary>
    [JsonPropertyName("hmds")]
    public HistoricalMarketDataStatus? HistoricalMarketData { get; init; }

    /// <summary>Brokerage session details.</summary>
    [JsonPropertyName("iserver")]
    public IServerStatus? IServer { get; init; }

    /// <summary>
    /// The reason the tickle was accepted but not processed, when IBKR reports one.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>The brokerage session status carried in the response, when present.</summary>
    public BrokerageSessionStatus? AuthenticationStatus => IServer?.AuthStatus;
}

/// <summary>Connection details for the historical market data server.</summary>
public sealed record HistoricalMarketDataStatus
{
    /// <summary>Any internal connection error.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>Brokerage session details carried in a tickle response.</summary>
public sealed record IServerStatus
{
    /// <summary>The brokerage session status.</summary>
    [JsonPropertyName("authStatus")]
    public BrokerageSessionStatus? AuthStatus { get; init; }
}
