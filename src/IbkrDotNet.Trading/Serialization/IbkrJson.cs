using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Serialization;

/// <summary>
/// The <see cref="JsonSerializerOptions"/> used for every Interactive Brokers Web API request and
/// response.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately absent: any global NodaTime converter. IBKR encodes time in at least six unrelated
/// formats and the correct one depends on the individual field, so every NodaTime-typed property on
/// a model must carry an explicit <see cref="JsonConverterAttribute"/> naming the converter for its
/// documented wire format. A property that forgets to do so fails loudly instead of silently being
/// read as ISO-8601 and producing a plausible but wrong value.
/// </para>
/// <para>
/// Unknown members are skipped rather than rejected: IBKR adds response fields without notice, and a
/// new field should not break an existing caller. For the same reason, string properties tolerate a
/// numeric or boolean value on the wire, which IBKR sends for several documented-as-string fields.
/// </para>
/// </remarks>
public static class IbkrJson
{
    /// <summary>
    /// The shared, read-only serializer options.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>
    /// Creates a fresh, mutable copy of the standard options, for callers that need to add their own
    /// converters.
    /// </summary>
    public static JsonSerializerOptions CreateMutableCopy() => new(Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // IBKR is inconsistent about casing across endpoints: 'accountId' and 'acctId' sit
            // alongside 'MAC' and 'hardware_info'. Models pin exact names with
            // [JsonPropertyName]; case insensitivity covers the remainder.
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // Order tickets carry a large number of optional fields. Omitting the nulls keeps
            // requests to what the caller actually set.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // A safety net for plain numeric properties: IBKR quotes numbers on many endpoints.
            NumberHandling = JsonNumberHandling.AllowReadingFromString,

            // New response fields should not break existing callers.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,

            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
        };

        options.Converters.Add(new JsonStringEnumConverter());

        // IBKR types string fields loosely: 'currency' and 'strike' are documented as strings but
        // arrive as numbers on several endpoints. This is a property of the API, not of particular
        // fields, so it is handled once here.
        options.Converters.Add(new FlexibleStringConverter());

        // Populate the reflection-based type resolver and freeze, so the instance is safe to share
        // across threads and cannot be mutated by a caller.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
