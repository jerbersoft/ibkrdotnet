using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Accounts;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="AccountSegmentSummary"/>.
/// </summary>
/// <remarks>
/// Nested values are coerced to strings: IBKR sends these maps as display text, but occasional
/// numeric and boolean entries appear alongside, and failing the whole summary over one of them
/// would be a poor trade.
/// </remarks>
public sealed class AccountSegmentSummaryConverter : JsonConverter<AccountSegmentSummary>
{
    /// <inheritdoc />
    public override AccountSegmentSummary Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var segments = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new AccountSegmentSummary(segments);
        }

        foreach (var segment in document.RootElement.EnumerateObject())
        {
            if (segment.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in segment.Value.EnumerateObject())
            {
                values[value.Name] = value.Value.ValueKind switch
                {
                    JsonValueKind.String => value.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    _ => value.Value.GetRawText(),
                };
            }

            segments[segment.Name] = values;
        }

        return new AccountSegmentSummary(segments);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        AccountSegmentSummary value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        foreach (var segment in value.Segments)
        {
            writer.WriteStartObject(segment.Key);
            foreach (var entry in segment.Value)
            {
                writer.WriteString(entry.Key, entry.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
