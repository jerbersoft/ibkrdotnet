using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="DateTimeZone"/> from an IANA time zone identifier such as
/// <c>America/New_York</c>, resolved against the TZDB provider.
/// </summary>
/// <remarks>
/// Used by the trading schedule's <c>timezone</c> and the alert <c>condition_time_zone</c>. The zone
/// is resolved eagerly so an unrecognised identifier surfaces at deserialization rather than at the
/// point some later calculation quietly produces the wrong local time.
/// </remarks>
public sealed class DateTimeZoneConverter : JsonConverter<DateTimeZone>
{
    private readonly IDateTimeZoneProvider _provider;

    /// <summary>Creates a converter using the TZDB zone provider.</summary>
    public DateTimeZoneConverter()
        : this(DateTimeZoneProviders.Tzdb)
    {
    }

    /// <summary>Creates a converter using a specific zone provider.</summary>
    /// <param name="provider">The provider used to resolve zone identifiers.</param>
    public DateTimeZoneConverter(IDateTimeZoneProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc />
    public override DateTimeZone Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var id = JsonReaderNumerics.ReadRequiredString(
            ref reader,
            "an IANA time zone identifier",
            typeof(DateTimeZone));

        return _provider.GetZoneOrNull(id)
            ?? throw IbkrSerializationException.ForValue(
                id,
                "an IANA time zone identifier known to the TZDB provider",
                typeof(DateTimeZone));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeZone value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Id);
    }
}
