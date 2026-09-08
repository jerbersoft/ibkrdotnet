using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads a list property that Interactive Brokers may send either as an array or as a bare object.
/// </summary>
/// <remarks>
/// <para>
/// The documented example for <c>GET /trsrv/secdef</c> shows <c>displayRule</c> as an array, but a
/// live gateway returns a single object for the same field on the same endpoint. Modelling either
/// shape alone fails against the other, so both are accepted and a single value is read as a
/// one-element list.
/// </para>
/// <para>
/// Applied per property rather than registered globally: a list arriving as an object is a quirk of
/// particular fields, and silently accepting it everywhere would hide a genuine schema mismatch.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class SingleOrArrayConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    /// <inheritdoc />
    public override IReadOnlyList<T>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartArray:
                return JsonSerializer.Deserialize<List<T>>(ref reader, options);

            case JsonTokenType.StartObject:
                var single = JsonSerializer.Deserialize<T>(ref reader, options);
                return single is null ? [] : [single];

            default:
                throw IbkrSerializationException.ForValue(
                    reader.TokenType.ToString(),
                    "an array or a single object",
                    typeof(IReadOnlyList<T>));
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        // Always written as an array: it is the shape the field is documented with, and the one that
        // round-trips whatever was read.
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
