using System.Text.Json;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="TradingScheduleDate"/> from IBKR's <c>yyyyMMdd</c> encoding,
/// recognising the marker dates that stand for a recurring weekday.
/// </summary>
public sealed class TradingScheduleDateConverter : IbkrStructConverterFactory<TradingScheduleDate>
{
    /// <inheritdoc />
    protected override TradingScheduleDate ReadValue(ref Utf8JsonReader reader)
    {
        var text = reader.TokenType == JsonTokenType.Number
            ? JsonReaderNumerics.ReadInt64(ref reader, "yyyyMMdd", typeof(TradingScheduleDate))
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : JsonReaderNumerics.ReadRequiredString(ref reader, "yyyyMMdd", typeof(TradingScheduleDate));

        var parsed = IbkrTimePatterns.Date.Parse(text);
        return parsed.Success
            ? TradingScheduleDate.ForDate(parsed.Value)
            : throw IbkrSerializationException.ForValue(text, "yyyyMMdd", typeof(TradingScheduleDate));
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, TradingScheduleDate value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(IbkrTimePatterns.Date.Format(value.ToWireDate()));
    }
}
