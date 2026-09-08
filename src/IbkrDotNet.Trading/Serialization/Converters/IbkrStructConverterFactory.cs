using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Base class for the value-type converters, so one <see cref="JsonConverterAttribute"/> serves both
/// <typeparamref name="T"/> and <c>T?</c> properties.
/// </summary>
/// <typeparam name="T">The value type being converted.</typeparam>
/// <remarks>
/// The nullable form also absorbs the textual stand-ins IBKR uses for an absent value — see
/// <see cref="IbkrNullSentinels"/> — which the non-nullable form cannot represent and so rejects.
/// </remarks>
public abstract class IbkrStructConverterFactory<T> : JsonConverterFactory
    where T : struct
{
    /// <inheritdoc />
    public sealed override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(T) || typeToConvert == typeof(T?);

    /// <inheritdoc />
    public sealed override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        typeToConvert == typeof(T)
            ? new ValueConverter(this)
            : new NullableConverter(this);

    /// <summary>Reads a present, non-null value.</summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    protected abstract T ReadValue(ref Utf8JsonReader reader);

    /// <summary>Writes a value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value to write.</param>
    protected abstract void WriteValue(Utf8JsonWriter writer, T value);

    /// <summary>Whether the reader is positioned on a value that means "absent".</summary>
    /// <param name="reader">The reader.</param>
    protected virtual bool IsAbsent(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ||
        (reader.TokenType == JsonTokenType.String && IbkrNullSentinels.IsNullish(reader.GetString()));

    private sealed class ValueConverter(IbkrStructConverterFactory<T> factory) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            factory.IsAbsent(ref reader)
                ? throw new IbkrSerializationException(
                    $"The value is absent, but {typeof(T).Name} cannot represent that. Declare the " +
                    $"property as {typeof(T).Name}? so an absent value can be read as null.")
                : factory.ReadValue(ref reader);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            factory.WriteValue(writer, value);
    }

    private sealed class NullableConverter(IbkrStructConverterFactory<T> factory) : JsonConverter<T?>
    {
        // Without this, a JSON null never reaches the converter and IBKR's textual stand-ins would
        // be the only absent form it saw.
        public override bool HandleNull => true;

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            factory.IsAbsent(ref reader) ? null : factory.ReadValue(ref reader);

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                factory.WriteValue(writer, value.Value);
            }
        }
    }
}
