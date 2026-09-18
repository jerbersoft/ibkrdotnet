using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Primitives;
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
/// <remarks>
/// <c>args</c> carries a list, for the same reason <c>ntf</c> does: IBKR's published example shows one
/// bare object and every bulletin a live gateway has sent arrived in an array, so both are read and a
/// single object is read as a one-element list.
/// </remarks>
public sealed record StreamingBulletin
{
    /// <summary>The topic, <c>blt</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The bulletins, usually one.</summary>
    [JsonPropertyName("args")]
    [JsonConverter(typeof(SingleOrArrayConverter<StreamingBulletinArgs>))]
    public IReadOnlyList<StreamingBulletinArgs> Args { get; init; } = [];
}

/// <summary>The body of a <see cref="StreamingBulletin"/>.</summary>
/// <remarks>
/// A live gateway also sends <c>exchanges</c>, which IBKR does not document. It has been null on every
/// bulletin captured, so there is nothing to say what it holds, and a property whose type was guessed
/// is worse than none: it is read and ignored until a bulletin arrives with a value in it.
/// </remarks>
public sealed record StreamingBulletinArgs
{
    /// <summary>The bulletin's identifier. Documented as a string and sent as a number.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The bulletin text.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>An <c>ntf</c> message: notices and prompts about trading activity.</summary>
/// <remarks>
/// <c>args</c> carries a list. IBKR's published example shows one bare object and a live gateway
/// sends an array, so both are read, and a single object is read as a one-element list — the same
/// accommodation <c>sor</c> and <c>str</c> need, and for the same reason.
/// </remarks>
public sealed record StreamingNotification
{
    /// <summary>The topic, <c>ntf</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The payloads, each a <see cref="StreamingNotificationArgs.Notice"/> or a
    /// <see cref="StreamingNotificationArgs.Prompt"/>.</summary>
    [JsonPropertyName("args")]
    [JsonConverter(typeof(SingleOrArrayConverter<StreamingNotificationArgs>))]
    public IReadOnlyList<StreamingNotificationArgs> Args { get; init; } = [];
}

/// <summary>
/// One payload of a <see cref="StreamingNotification"/>: a notice or a prompt.
/// </summary>
/// <remarks>
/// <para>
/// IBKR sends two unrelated things under the one topic. A notice reports something that happened and
/// asks nothing — a warning that a resting order will be cancelled at a date, say. A prompt is a
/// question about a named order, the same question the submission reply loop answers over REST,
/// arriving unsolicited because something other than this client provoked it.
/// </para>
/// <para>
/// They are separate types rather than one record with everything optional, so that answering a
/// prompt does not begin with working out whether the payload was one.
/// </para>
/// </remarks>
[JsonConverter(typeof(StreamingNotificationArgsConverter))]
public abstract record StreamingNotificationArgs
{
    private StreamingNotificationArgs()
    {
    }

    /// <summary>The message text. The body of a notice, the question of a prompt.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>A notice: something that happened, with nothing to answer.</summary>
    public sealed record Notice : StreamingNotificationArgs
    {
        /// <summary>The notice's identifier, which is IBKR's warning number.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>The headline. Null on every recorded notice; IBKR's example fills it.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        /// <summary>Where to read more, when there is somewhere.</summary>
        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }

    /// <summary>A prompt: a question about an order, awaiting one of its options.</summary>
    /// <remarks>
    /// Answer it with <c>IOrdersClient.DismissServerPromptAsync</c>, passing <see cref="OrderId"/>,
    /// <see cref="RequestId"/> and the chosen entry of <see cref="Options"/>. Leaving it unanswered
    /// is a decision too: the order stands as IBKR already has it.
    /// </remarks>
    public sealed record Prompt : StreamingNotificationArgs
    {
        /// <summary>The order the question is about, by IBKR's own numeric identifier.</summary>
        [JsonPropertyName("orderId")]
        public OrderId? OrderId { get; init; }

        /// <summary>IBKR's identifier for the question, passed back when it is answered.</summary>
        [JsonPropertyName("reqId")]
        public string? RequestId { get; init; }

        /// <summary>
        /// IBKR's identifier for the category of question, for example <c>p12</c>.
        /// </summary>
        /// <remarks>
        /// The same categories <c>IOrdersClient.SuppressMessagesAsync</c> silences for the brokerage
        /// session, so a question answered the same way every time need not be asked again.
        /// </remarks>
        [JsonPropertyName("messageId")]
        public string? MessageId { get; init; }

        /// <summary>The answers on offer, one of which is sent back verbatim.</summary>
        [JsonPropertyName("options")]
        public IReadOnlyList<string> Options { get; init; } = [];

        /// <summary>The message type, for example <c>M</c>.</summary>
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        /// <summary>The ways the question may be dismissed without answering it.</summary>
        [JsonPropertyName("dismissable")]
        public IReadOnlyList<string> Dismissable { get; init; } = [];

        /// <summary>IBKR's own marker that this payload is a question. Set on every one recorded.</summary>
        [JsonPropertyName("prompt")]
        public bool? IsPrompt { get; init; }
    }
}
