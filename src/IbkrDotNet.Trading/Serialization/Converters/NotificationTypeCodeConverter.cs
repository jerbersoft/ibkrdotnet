using System.Text.Json;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="NotificationTypeCode"/>.
/// </summary>
public sealed class NotificationTypeCodeConverter : IbkrStructConverterFactory<NotificationTypeCode>
{
    /// <inheritdoc />
    protected override NotificationTypeCode ReadValue(ref Utf8JsonReader reader) =>
        new(JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "a notification type code",
            typeof(NotificationTypeCode)));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, NotificationTypeCode value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}
