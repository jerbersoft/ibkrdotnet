using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="AccountId"/>.
/// </summary>
public sealed class AccountIdConverter : JsonConverter<AccountId>
{
    /// <inheritdoc />
    public override AccountId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "an account identifier",
            typeof(AccountId)));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AccountId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc />
    public override AccountId ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    /// <inheritdoc />
    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        AccountId value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value);
    }
}
