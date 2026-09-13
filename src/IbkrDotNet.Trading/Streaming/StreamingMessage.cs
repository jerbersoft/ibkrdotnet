using System.Text.Json;
using IbkrDotNet.Trading.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// One JSON message received from the WebSocket.
/// </summary>
/// <remarks>
/// Every message IBKR sends is a JSON object with a <c>topic</c> field, and that is all the transport
/// assumes about it. The body is kept whole so a topic client can read it as its own type through
/// <see cref="Deserialize{T}"/>, and so a caller reaching a topic this library has not modelled can
/// read it as a <see cref="JsonElement"/>.
/// </remarks>
public sealed class StreamingMessage
{
    /// <summary>Creates a message.</summary>
    /// <param name="topic">The <c>topic</c> field.</param>
    /// <param name="body">The whole message.</param>
    /// <param name="receivedAt">When the transport read it.</param>
    public StreamingMessage(string topic, JsonElement body, Instant receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        Topic = topic;
        Body = body;
        ReceivedAt = receivedAt;
    }

    /// <summary>The <c>topic</c> field, for example <c>smd+8314</c> or <c>sts</c>.</summary>
    public string Topic { get; }

    /// <summary>The whole message, <c>topic</c> included.</summary>
    public JsonElement Body { get; }

    /// <summary>When the transport read the message, on the injected clock.</summary>
    public Instant ReceivedAt { get; }

    /// <summary>Reads the message as a model type, with the library's usual JSON options.</summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <exception cref="IbkrSerializationException">The body could not be read as <typeparamref name="T"/>.</exception>
    public T Deserialize<T>()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Body, IbkrJson.Options)
                ?? throw new IbkrSerializationException(
                    $"The '{Topic}' message is a JSON null where a {typeof(T).Name} was expected.");
        }
        catch (JsonException ex)
        {
            throw new IbkrSerializationException(
                $"The '{Topic}' message could not be read as {typeof(T).Name}: {ex.Message}",
                ex);
        }
    }

    /// <summary>The message as IBKR sent it.</summary>
    public override string ToString() => Body.GetRawText();
}
