using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a string property whose value Interactive Brokers may send as a number or a boolean.
/// </summary>
/// <remarks>
/// <para>
/// IBKR types these loosely and inconsistently. The portfolio summary documents <c>currency</c> as a
/// string but sends it as a number on most entries; <c>strike</c> is documented as a string and
/// arrives as a number for a zero strike. Without this, one such field fails the entire response.
/// </para>
/// <para>
/// Registered globally in <see cref="IbkrJson"/> rather than applied field by field, because the
/// looseness is a property of the API rather than of particular fields, and the next endpoint to do
/// it should not need a code change.
/// </para>
/// </remarks>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Number => ReadNumber(ref reader),
            _ => throw IbkrSerializationException.ForValue(
                reader.TokenType.ToString(),
                "a string, number or boolean",
                typeof(string)),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }

    /// <inheritdoc />
    public override string ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.GetString() ?? string.Empty;

    /// <inheritdoc />
    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value);
    }

    private static string ReadNumber(ref Utf8JsonReader reader)
    {
        // Preserve the number exactly as it appeared, rather than round-tripping through a numeric
        // type and risking a changed representation.
        return reader.HasValueSequence
            ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
            : Encoding.UTF8.GetString(reader.ValueSpan);
    }
}
