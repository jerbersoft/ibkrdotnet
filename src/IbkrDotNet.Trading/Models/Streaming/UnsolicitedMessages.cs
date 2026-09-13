using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Streaming;

/// <summary>
/// A <c>system</c> message: the confirmation IBKR sends when the socket opens, and the heartbeat
/// it sends every ten seconds afterwards.
/// </summary>
public sealed record StreamingSystemMessage
{
    /// <summary>The topic, <c>system</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The username the socket was opened for, on the confirmation.</summary>
    [JsonPropertyName("success")]
    public string? Success { get; init; }

    /// <summary>The server's time, on a heartbeat.</summary>
    [JsonPropertyName("hb")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? Heartbeat { get; init; }

    /// <summary>Whether this is a heartbeat rather than the confirmation.</summary>
    public bool IsHeartbeat => Heartbeat is not null;
}

/// <summary>
/// An <c>sts</c> message: the brokerage session's authentication status, sent when the socket opens
/// and again whenever it changes, for instance when another platform competes for the session.
/// </summary>
public sealed record StreamingAuthenticationStatus
{
    /// <summary>The topic, <c>sts</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The status. IBKR documents only <c>authenticated</c>; the rest arrives when it does.</summary>
    [JsonPropertyName("args")]
    public BrokerageSessionStatus? Args { get; init; }
}

/// <summary>A <c>blt</c> message: an urgent bulletin about an exchange or system issue.</summary>
public sealed record StreamingBulletin
{
    /// <summary>The topic, <c>blt</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The bulletin.</summary>
    [JsonPropertyName("args")]
    public StreamingBulletinArgs? Args { get; init; }
}

/// <summary>The body of a <see cref="StreamingBulletin"/>.</summary>
public sealed record StreamingBulletinArgs
{
    /// <summary>The bulletin's identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The bulletin text.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>An <c>ntf</c> message: a brief notification about trading activity.</summary>
public sealed record StreamingNotification
{
    /// <summary>The topic, <c>ntf</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The notification.</summary>
    [JsonPropertyName("args")]
    public StreamingNotificationArgs? Args { get; init; }
}

/// <summary>The body of a <see cref="StreamingNotification"/>.</summary>
public sealed record StreamingNotificationArgs
{
    /// <summary>The notification's identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The headline.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>The body text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>Where to read more, when there is somewhere.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
