using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="LocalDate"/> encoded as <c>yyyyMMdd</c>.
/// </summary>
/// <remarks>
/// Used by <c>expiry</c>, <c>maturityDate</c>, <c>maturity_date</c> and the various <c>date</c>
/// fields. These are calendar dates with no time or zone component, which is exactly what
/// <see cref="LocalDate"/> models. Values that arrive as JSON numbers (IBKR does this for
/// <c>expirationDate</c> and <c>ltd</c>) are accepted.
/// </remarks>
public sealed class IbkrLocalDateConverter : JsonConverter<LocalDate>
{
    /// <inheritdoc />
    public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.Number
            ? JsonReaderNumerics.ReadInt64(ref reader, "yyyyMMdd", typeof(LocalDate))
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : JsonReaderNumerics.ReadRequiredString(ref reader, "yyyyMMdd", typeof(LocalDate));

        var parsed = IbkrTimePatterns.Date.Parse(text);
        if (!parsed.Success)
        {
            throw IbkrSerializationException.ForValue(text, "yyyyMMdd", typeof(LocalDate));
        }

        return parsed.Value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(IbkrTimePatterns.Date.Format(value));
    }
}
