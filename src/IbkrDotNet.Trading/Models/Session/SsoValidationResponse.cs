using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Session;

/// <summary>The response from <c>GET /sso/validate</c>.</summary>
/// <remarks>
/// This validates the outer read-only Web API session, which every request depends on, rather than
/// the brokerage session that gates <c>/iserver</c>. Limited to one request per minute.
/// </remarks>
public sealed record SsoValidationResponse
{
    /// <summary>Whether the session is valid.</summary>
    [JsonPropertyName("RESULT")]
    public bool Result { get; init; }

    /// <summary>The authenticated username.</summary>
    [JsonPropertyName("USER_NAME")]
    public string? UserName { get; init; }

    /// <summary>The authenticated user's identifier.</summary>
    [JsonPropertyName("USER_ID")]
    public long? UserId { get; init; }

    /// <summary>The credential the session was established with.</summary>
    [JsonPropertyName("CREDENTIAL")]
    public string? Credential { get; init; }

    /// <summary>When the session was authenticated.</summary>
    [JsonPropertyName("AUTH_TIME")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? AuthenticatedAt { get; init; }

    /// <summary>
    /// When the SSO session expires.
    /// </summary>
    /// <remarks>
    /// Not a plain <see cref="Duration"/>: IBKR documents this as milliseconds remaining but a live
    /// Client Portal Gateway sends an epoch-millisecond timestamp. See <see cref="SsoExpiry"/>.
    /// </remarks>
    [JsonPropertyName("EXPIRES")]
    [JsonConverter(typeof(SsoExpiryConverter))]
    public SsoExpiry? Expires { get; init; }

    /// <summary>When the user was last active.</summary>
    [JsonPropertyName("lastAccessed")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? LastAccessed { get; init; }

    /// <summary>The IP address the session is bound to.</summary>
    [JsonPropertyName("IP")]
    public string? IpAddress { get; init; }

    /// <summary>Whether the account is a free trial.</summary>
    [JsonPropertyName("IS_FREE_TRIAL")]
    public bool? IsFreeTrial { get; init; }

    /// <summary>Whether the username is a master user.</summary>
    [JsonPropertyName("IS_MASTER")]
    public bool? IsMaster { get; init; }

    /// <summary>The paper trading username, when one exists.</summary>
    [JsonPropertyName("PAPER_USER_NAME")]
    public string? PaperUserName { get; init; }

    /// <summary>The landing application.</summary>
    [JsonPropertyName("LANDING_APP")]
    public string? LandingApp { get; init; }

    /// <summary>The login type.</summary>
    [JsonPropertyName("LOGIN_TYPE")]
    public int? LoginType { get; init; }

    /// <summary>The IBKR region serving the session.</summary>
    [JsonPropertyName("region")]
    public string? Region { get; init; }

    /// <summary>Feature flags enabled for the session.</summary>
    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, object>? Features { get; init; }
}

/// <summary>The response from <c>POST /logout</c>.</summary>
public sealed record LogoutResponse
{
    /// <summary>Whether the session was terminated.</summary>
    [JsonPropertyName("status")]
    public bool Status { get; init; }
}

/// <summary>The body of <c>POST /iserver/auth/ssodh/init</c>.</summary>
/// <param name="Compete">
/// Whether to take over the brokerage session from any other platform holding it. A username may
/// hold only one at a time, so setting this displaces an existing Trader Workstation or Client
/// Portal session rather than failing.
/// </param>
/// <param name="Publish">
/// Whether to publish the brokerage session token at initialization. IBKR documents <c>true</c> as
/// the preferred value; otherwise the token must be published before calling this.
/// </param>
public sealed record InitializeBrokerageSessionRequest(
    [property: JsonPropertyName("compete")] bool Compete,
    [property: JsonPropertyName("publish")] bool Publish);
