using System.Text;
using System.Text.Json;
using IbkrDotNet.Trading.Serialization;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// Renders the text frames IBKR's WebSocket reads: <c>TOPIC+[TARGET]+{PARAMETERS}</c>.
/// </summary>
/// <remarks>
/// The plus sign separates the parts, the target is optional, and the parameters are a JSON object
/// or absent altogether. <c>smd+8314+{"fields":["31"]}</c> subscribes to IBM's top of book,
/// <c>sor+{"filters":["Submitted"]}</c> has no target, and <c>tic</c> has neither.
/// </remarks>
public static class StreamingFrame
{
    /// <summary>The separator between a frame's parts.</summary>
    public const char Separator = '+';

    /// <summary>
    /// An empty parameter object, for topics that want <c>{}</c> rather than nothing.
    /// </summary>
    public static object EmptyParameters { get; } = new Dictionary<string, object>(0);

    /// <summary>Renders a frame.</summary>
    /// <param name="topic">The topic, for example <c>smd</c>.</param>
    /// <param name="target">The target, for example a contract identifier, or <see langword="null"/> for none.</param>
    /// <param name="parameters">
    /// The parameters, serialized as JSON with the library's usual options, or <see langword="null"/>
    /// to send none. Pass <see cref="EmptyParameters"/> for a literal <c>{}</c>.
    /// </param>
    public static string Encode(string topic, string? target = null, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var frame = new StringBuilder(topic);
        if (target is { Length: > 0 })
        {
            frame.Append(Separator).Append(target);
        }

        if (parameters is not null)
        {
            frame.Append(Separator)
                .Append(JsonSerializer.Serialize(parameters, parameters.GetType(), IbkrJson.Options));
        }

        return frame.ToString();
    }
}
