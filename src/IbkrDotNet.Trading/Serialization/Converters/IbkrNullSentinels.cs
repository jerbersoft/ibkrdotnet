namespace IbkrDotNet.Trading.Serialization.Converters;

/// <summary>
/// The textual stand-ins Interactive Brokers uses for an absent value.
/// </summary>
/// <remarks>
/// IBKR does not consistently use JSON <c>null</c>. A non-expiring instrument may report
/// <c>"expiry": null</c> on one endpoint and <c>"expiry": "None"</c> on another, and empty strings
/// appear in the same role. Treating these as absent is materially better than failing the whole
/// response or, worse, parsing them into a wrong value.
/// </remarks>
internal static class IbkrNullSentinels
{
    public static bool IsNullish(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("None", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("N/A", StringComparison.OrdinalIgnoreCase);
}
