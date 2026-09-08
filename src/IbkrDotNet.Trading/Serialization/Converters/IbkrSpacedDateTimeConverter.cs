using System.Text.Json;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="Instant"/> encoded as <c>yyyy-MM-dd HH:mm:ss</c> in UTC.
/// </summary>
/// <remarks>
/// Used by the PortfolioAnalyst all-periods <c>lastSuccessfulUpdate</c>, which is the only field on
/// the API in this format. IBKR does not say what zone it is in; it is read as UTC because a live
/// gateway answers with UTC's wall clock rather than the host's -- two requests fired at 17:33:08
/// and 17:48:45 UTC from a machine an hour ahead came back reading exactly those times.
/// </remarks>
public sealed class IbkrSpacedDateTimeConverter : IbkrStructConverterFactory<Instant>
{
    /// <inheritdoc />
    protected override Instant ReadValue(ref Utf8JsonReader reader)
    {
        var text = JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "yyyy-MM-dd HH:mm:ss",
            typeof(Instant));

        var parsed = IbkrTimePatterns.SpacedDateTime.Parse(text);
        return parsed.Success
            ? parsed.Value.InUtc().ToInstant()
            : throw IbkrSerializationException.ForValue(text, "yyyy-MM-dd HH:mm:ss", typeof(Instant));
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, Instant value) =>
        writer.WriteStringValue(IbkrTimePatterns.SpacedDateTime.Format(value.InUtc().LocalDateTime));
}
