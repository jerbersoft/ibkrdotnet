using System.Text.Json;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="IsoDayOfWeek"/> from its English name, such as <c>Saturday</c>.
/// </summary>
/// <remarks>
/// Used by the event contract trading schedule's <c>day_of_week</c>. Unlike most of IBKR's
/// enumerations this one really is closed -- there are seven days -- so an unrecognised name is a
/// deserialization failure rather than a value carried through.
/// </remarks>
public sealed class IsoDayOfWeekConverter : IbkrStructConverterFactory<IsoDayOfWeek>
{
    /// <inheritdoc />
    protected override IsoDayOfWeek ReadValue(ref Utf8JsonReader reader)
    {
        var text = JsonReaderNumerics.ReadRequiredString(
            ref reader, "a day of the week", typeof(IsoDayOfWeek));

        // Rejects "None" as well as anything unrecognised: IsoDayOfWeek.None is the enum's absent
        // value, and reading it back from the wire would claim IBKR named a day it did not.
        return Enum.TryParse<IsoDayOfWeek>(text, ignoreCase: true, out var day)
            && day is not IsoDayOfWeek.None
                ? day
                : throw IbkrSerializationException.ForValue(
                    text, "a day of the week", typeof(IsoDayOfWeek));
    }

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, IsoDayOfWeek value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
