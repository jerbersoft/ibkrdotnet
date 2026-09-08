using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes an <see cref="AccountPerformanceHistory"/>, whose periods arrive as property
/// names rather than as entries in a list.
/// </summary>
/// <remarks>
/// <c>1D</c> and <c>1Y</c> sit as siblings of <c>baseCurrency</c> and <c>start</c> in one object, so
/// the five documented fields are bound by name and everything else holding an object is taken to be
/// a period. Which periods appear varies by account, which is the other reason they cannot be
/// properties.
/// </remarks>
public sealed class AccountPerformanceHistoryConverter : JsonConverter<AccountPerformanceHistory>
{
    /// <inheritdoc />
    public override AccountPerformanceHistory Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw IbkrSerializationException.ForValue(
                root.ValueKind.ToString(),
                "an object",
                typeof(AccountPerformanceHistory));
        }

        // Attached before it is filled; see PerformanceAllPeriodsConverter for why that is safe.
        var periods = new Dictionary<string, PerformancePeriodSeries>(StringComparer.Ordinal);
        var result = new AccountPerformanceHistory { Periods = periods };

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "baseCurrency":
                    result = result with { BaseCurrency = property.Value.Deserialize<string>(options) };
                    break;
                case "start":
                    result = result with { Start = ReadDate(property.Value) };
                    break;
                case "end":
                    result = result with { End = ReadDate(property.Value) };
                    break;
                case "lastSuccessfulUpdate":
                    result = result with { LastSuccessfulUpdate = ReadUpdatedAt(property.Value) };
                    break;
                case "periods":
                    result = result with
                    {
                        PeriodNames = property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.Deserialize<IReadOnlyList<string>>(options) ?? []
                            : [],
                    };
                    break;
                default:
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        periods[property.Name] =
                            property.Value.Deserialize<PerformancePeriodSeries>(options)
                            ?? new PerformancePeriodSeries();
                    }

                    break;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        AccountPerformanceHistory value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach (var period in value.Periods)
        {
            writer.WritePropertyName(period.Key);
            JsonSerializer.Serialize(writer, period.Value, options);
        }

        if (value.BaseCurrency is not null)
        {
            writer.WriteString("baseCurrency", value.BaseCurrency);
        }

        if (value.Start is { } start)
        {
            writer.WriteString("start", IbkrTimePatterns.Date.Format(start));
        }

        if (value.End is { } end)
        {
            writer.WriteString("end", IbkrTimePatterns.Date.Format(end));
        }

        if (value.PeriodNames.Count > 0)
        {
            writer.WriteStartArray("periods");
            foreach (var name in value.PeriodNames)
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
        }

        if (value.LastSuccessfulUpdate is { } updated)
        {
            writer.WriteString(
                "lastSuccessfulUpdate",
                IbkrTimePatterns.SpacedDateTime.Format(updated.InUtc().LocalDateTime));
        }

        writer.WriteEndObject();
    }

    private static LocalDate? ReadDate(JsonElement element)
    {
        if (ReadText(element) is not { } text)
        {
            return null;
        }

        var parsed = IbkrTimePatterns.Date.Parse(text);
        return parsed.Success
            ? parsed.Value
            : throw IbkrSerializationException.ForValue(text, "yyyyMMdd", typeof(LocalDate));
    }

    private static Instant? ReadUpdatedAt(JsonElement element)
    {
        if (ReadText(element) is not { } text)
        {
            return null;
        }

        var parsed = IbkrTimePatterns.SpacedDateTime.Parse(text);
        return parsed.Success
            ? parsed.Value.InUtc().ToInstant()
            : throw IbkrSerializationException.ForValue(text, "yyyy-MM-dd HH:mm:ss", typeof(Instant));
    }

    private static string? ReadText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.GetRawText();

        return IbkrNullSentinels.IsNullish(text) ? null : text;
    }
}
