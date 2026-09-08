using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Session;

/// <summary>
/// The state of the brokerage session, as reported by <c>/iserver/auth/status</c> and
/// <c>/iserver/auth/ssodh/init</c>.
/// </summary>
/// <remarks>
/// The four flags describe different things and should not be collapsed into one notion of "logged
/// in": a session can be <see cref="Connected"/> and <see cref="Authenticated"/> while still not
/// <see cref="Established"/>, in which case account information has not loaded yet and requests for
/// it will come back empty rather than fail.
/// </remarks>
public sealed record BrokerageSessionStatus
{
    /// <summary>Whether there is a physical connection to IBKR's brokerage infrastructure.</summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; init; }

    /// <summary>
    /// Whether initial authentication has passed. The session may not be fully initialized yet.
    /// </summary>
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    /// <summary>
    /// Whether the session is fully initialized, with account information loaded, and ready to
    /// handle requests.
    /// </summary>
    /// <remarks>
    /// Set once the login message arrives from IBKR's backend. This, not
    /// <see cref="Authenticated"/>, is the flag to gate trading on.
    /// </remarks>
    [JsonPropertyName("established")]
    public bool Established { get; init; }

    /// <summary>
    /// Whether another session is competing for the same username.
    /// </summary>
    /// <remarks>
    /// An IBKR username can hold only one brokerage session at a time across all platforms, so a
    /// competing session means someone (or something) else has logged in — Trader Workstation or
    /// Client Portal, for instance — and this session may be displaced.
    /// </remarks>
    [JsonPropertyName("competing")]
    public bool Competing { get; init; }

    /// <summary>Any message accompanying the authentication status.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>The reason authentication status could not be retrieved, when it could not.</summary>
    [JsonPropertyName("fail")]
    public string? Fail { get; init; }

    /// <summary>Device MAC information reported by IBKR.</summary>
    [JsonPropertyName("MAC")]
    public string? Mac { get; init; }

    /// <summary>Client Portal use only.</summary>
    [JsonPropertyName("hardware_info")]
    public string? HardwareInfo { get; init; }

    /// <summary>IBKR server details. Internal use.</summary>
    [JsonPropertyName("serverInfo")]
    public BrokerageServerInfo? ServerInfo { get; init; }

    /// <summary>
    /// Whether the session is ready to place orders and consume market data.
    /// </summary>
    public bool IsReadyToTrade => Connected && Authenticated && Established;
}

/// <summary>IBKR server details. Internal use.</summary>
public sealed record BrokerageServerInfo
{
    /// <summary>The server name.</summary>
    [JsonPropertyName("serverName")]
    public string? ServerName { get; init; }

    /// <summary>The server version.</summary>
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; init; }
}
