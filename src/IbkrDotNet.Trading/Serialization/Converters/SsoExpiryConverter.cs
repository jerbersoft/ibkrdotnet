using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// Reads the <c>EXPIRES</c> field of <c>GET /sso/validate</c>, which IBKR sends either as a
/// duration in milliseconds or as an epoch-millisecond timestamp.
/// </summary>
/// <remarks>
/// See <see cref="SsoExpiry"/> for why both shapes occur and how they are told apart. Values are
/// written back exactly as they were read, so a round-trip does not quietly change which shape a
/// downstream reader sees.
/// </remarks>
public sealed class SsoExpiryConverter : IbkrStructConverterFactory<SsoExpiry>
{
    /// <inheritdoc />
    protected override SsoExpiry ReadValue(ref Utf8JsonReader reader) =>
        SsoExpiry.FromMilliseconds(
            JsonReaderNumerics.ReadInt64(ref reader, "a number of milliseconds", typeof(SsoExpiry)));

    /// <inheritdoc />
    protected override void WriteValue(Utf8JsonWriter writer, SsoExpiry value)
    {
        writer.WriteNumberValue(value.TotalMilliseconds);
    }
}
