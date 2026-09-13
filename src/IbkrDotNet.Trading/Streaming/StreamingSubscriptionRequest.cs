namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// Describes a topic to receive from the WebSocket: what to send to open it, what to send to close
/// it, and which inbound messages belong to it.
/// </summary>
/// <remarks>
/// <para>
/// Build one with <see cref="Solicited"/> for a topic IBKR has to be asked for, such as
/// <c>smd</c>, or <see cref="Unsolicited"/> for one it sends on its own, such as <c>sts</c>. The
/// transport keeps the request for as long as the subscription lives, because it is what gets
/// re-sent when the socket reconnects.
/// </para>
/// <para>
/// Inbound messages are matched on their <c>topic</c> field against <see cref="RoutingKey"/>: for
/// <c>smd+8314+{...}</c> the responses say <c>"topic":"smd+8314"</c>, for <c>sor+{...}</c> they say
/// <c>"topic":"sor"</c>. A topic whose responses carry something other than the target, as
/// historical data does with its server identifier, sets <see cref="MatchResponseTopicPrefix"/>.
/// </para>
/// </remarks>
public sealed record StreamingSubscriptionRequest
{
    /// <summary>The topic, for example <c>smd</c>. See <see cref="StreamingTopics"/>.</summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The target, for example a contract identifier or account, or <see langword="null"/> when the
    /// topic takes none.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// The parameters serialized into the subscribe frame, or <see langword="null"/> to send none.
    /// </summary>
    public object? Parameters { get; init; }

    /// <summary>
    /// Whether the transport sends a subscribe frame to open this topic. <see langword="false"/> for
    /// an unsolicited topic, which is only listened for.
    /// </summary>
    public bool IsSolicited { get; init; } = true;

    /// <summary>
    /// The topic that cancels this one, for example <c>umd</c>, sent with the same target and empty
    /// parameters when the subscription is disposed. <see langword="null"/> sends nothing.
    /// </summary>
    public string? UnsubscribeTopic { get; init; }

    /// <summary>
    /// The <c>topic</c> value inbound messages carry, when it is not simply the topic and target
    /// joined with <c>+</c>.
    /// </summary>
    public string? ResponseTopic { get; init; }

    /// <summary>
    /// Whether an inbound topic that starts with <see cref="RoutingKey"/> followed by <c>+</c> also
    /// belongs to this subscription, for responses that append a value the request could not know.
    /// </summary>
    public bool MatchResponseTopicPrefix { get; init; }

    /// <summary>
    /// How many messages this subscription buffers for a consumer that has not read them yet,
    /// overriding <see cref="Configuration.IbkrStreamingOptions.BufferCapacity"/>.
    /// </summary>
    public int? BufferCapacity { get; init; }

    /// <summary>The inbound <c>topic</c> value that routes messages to this subscription.</summary>
    public string RoutingKey =>
        ResponseTopic ?? (Target is { Length: > 0 } ? $"{Topic}{StreamingFrame.Separator}{Target}" : Topic);

    /// <summary>The frame that opens the topic, or <see langword="null"/> for an unsolicited one.</summary>
    public string? SubscribeFrame =>
        IsSolicited ? StreamingFrame.Encode(Topic, Target, Parameters) : null;

    /// <summary>The frame that closes the topic, or <see langword="null"/> when there is none.</summary>
    public string? UnsubscribeFrame =>
        UnsubscribeTopic is { Length: > 0 }
            ? StreamingFrame.Encode(UnsubscribeTopic, Target, StreamingFrame.EmptyParameters)
            : null;

    /// <summary>
    /// A topic IBKR has to be asked for. The unsubscribe topic is derived by IBKR's convention of
    /// swapping the leading <c>s</c> for a <c>u</c>.
    /// </summary>
    /// <param name="topic">The topic, for example <c>smd</c>.</param>
    /// <param name="target">The target, or <see langword="null"/> when the topic takes none.</param>
    /// <param name="parameters">
    /// The parameters, or <see langword="null"/> for an empty <c>{}</c>. IBKR expects the object
    /// even when it is empty.
    /// </param>
    public static StreamingSubscriptionRequest Solicited(string topic, string? target = null, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (topic.Length != 3 || topic[0] != 's')
        {
            throw new ArgumentException(
                $"'{topic}' is not a solicited topic. IBKR's solicited topics are three characters " +
                "starting with 's', such as 'smd'; build the request directly for anything else.",
                nameof(topic));
        }

        return new StreamingSubscriptionRequest
        {
            Topic = topic,
            Target = target,
            Parameters = parameters ?? StreamingFrame.EmptyParameters,
            UnsubscribeTopic = string.Concat("u", topic.AsSpan(1)),
        };
    }

    /// <summary>A topic IBKR sends on its own, which is only listened for.</summary>
    /// <param name="topic">The topic, for example <c>sts</c>.</param>
    public static StreamingSubscriptionRequest Unsolicited(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return new StreamingSubscriptionRequest { Topic = topic, IsSolicited = false };
    }

    /// <summary>Whether an inbound message with this <c>topic</c> belongs to the subscription.</summary>
    /// <param name="inboundTopic">The <c>topic</c> field of the message.</param>
    public bool Matches(string inboundTopic)
    {
        ArgumentNullException.ThrowIfNull(inboundTopic);

        var key = RoutingKey;
        if (string.Equals(inboundTopic, key, StringComparison.Ordinal))
        {
            return true;
        }

        return MatchResponseTopicPrefix
            && inboundTopic.Length > key.Length
            && inboundTopic[key.Length] == StreamingFrame.Separator
            && inboundTopic.StartsWith(key, StringComparison.Ordinal);
    }
}
