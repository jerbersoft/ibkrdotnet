using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads and writes a <see cref="PerformanceAllPeriods"/>, whose figures arrive under a property
/// named after the account rather than under a fixed one.
/// </summary>
/// <remarks>
/// Property binding cannot express that, and IBKR's reference does not admit it happens: the
/// documented response is seven scalar fields, none of which is the data. Any property holding an
/// object is taken to be an account, which is what distinguishes <c>DU1234567</c> from the fields
/// that are documented -- every one of those is a string, a number or an array.
/// </remarks>
public sealed class PerformanceAllPeriodsConverter : JsonConverter<PerformanceAllPeriods>
{
    /// <inheritdoc />
    public override PerformanceAllPeriods Read(
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
                typeof(PerformanceAllPeriods));
        }

        // Attached before it is filled: the 'with' expressions below copy the reference, so the
        // entries added while walking the object land on whichever copy is returned.
        var accounts = new Dictionary<AccountId, AccountPerformanceHistory>();
        var result = new PerformanceAllPeriods { Accounts = accounts };

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    result = result with { Id = property.Value.Deserialize<string>(options) };
                    break;
                case "rc":
                    result = result with { ResponseCode = property.Value.Deserialize<long?>(options) };
                    break;
                case "nd":
                    result = result with { DayCount = property.Value.Deserialize<long?>(options) };
                    break;
                case "pm":
                    result = result with { PortfolioMeasure = property.Value.Deserialize<string>(options) };
                    break;
                case "currencyType":
                    result = result with { CurrencyType = property.Value.Deserialize<string>(options) };
                    break;
                case "view":
                    result = result with { View = ReadAccounts(property.Value, options) };
                    break;
                case "included":
                    result = result with { Included = ReadAccounts(property.Value, options) };
                    break;
                default:
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        accounts[new AccountId(property.Name)] =
                            property.Value.Deserialize<AccountPerformanceHistory>(options)
                            ?? new AccountPerformanceHistory();
                    }

                    break;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        PerformanceAllPeriods value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        WriteIfPresent(writer, "id", value.Id);
        WriteIfPresent(writer, "rc", value.ResponseCode);
        WriteIfPresent(writer, "nd", value.DayCount);
        WriteIfPresent(writer, "pm", value.PortfolioMeasure);
        WriteIfPresent(writer, "currencyType", value.CurrencyType);
        WriteAccounts(writer, "view", value.View);
        WriteAccounts(writer, "included", value.Included);

        foreach (var account in value.Accounts)
        {
            writer.WritePropertyName(account.Key.Value);
            JsonSerializer.Serialize(writer, account.Value, options);
        }

        writer.WriteEndObject();
    }

    private static IReadOnlyList<AccountId> ReadAccounts(JsonElement element, JsonSerializerOptions options) =>
        element.ValueKind == JsonValueKind.Array
            ? element.Deserialize<IReadOnlyList<AccountId>>(options) ?? []
            : [];

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static void WriteAccounts(Utf8JsonWriter writer, string name, IReadOnlyList<AccountId> accounts)
    {
        if (accounts.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var account in accounts)
        {
            writer.WriteStringValue(account.Value);
        }

        writer.WriteEndArray();
    }
}
