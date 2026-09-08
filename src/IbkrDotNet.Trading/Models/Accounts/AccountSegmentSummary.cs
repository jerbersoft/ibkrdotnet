using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;

namespace IbkrDotNet.Trading.Models.Accounts;

/// <summary>
/// A summary keyed by account segment or currency, as returned by the four
/// <c>/iserver/account/{accountId}/summary/*</c> endpoints.
/// </summary>
/// <remarks>
/// <para>
/// IBKR returns these as nested maps whose inner keys are display labels rather than a stable
/// schema — <c>net_liquidation</c> sits alongside <c>Prvs Dy Eqty Wth Ln Vl</c> and
/// <c>Nt Lqdtn Uncrtnty</c> — and the set of keys varies by segment, by account type and over time.
/// Modelling every label as a property would be wrong within a release, so the map is exposed as it
/// arrives, with named accessors for the segments IBKR does document.
/// </para>
/// <para>
/// Values are display-formatted (<c>"1,288,301 USD"</c>). Use <see cref="GetAmount"/> to read one as
/// a number.
/// </para>
/// </remarks>
[JsonConverter(typeof(AccountSegmentSummaryConverter))]
public sealed class AccountSegmentSummary
{
    /// <summary>Creates a summary from its segments.</summary>
    /// <param name="segments">The segment map as IBKR returned it.</param>
    public AccountSegmentSummary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        Segments = segments;
    }

    /// <summary>The segments, keyed as IBKR returned them.</summary>
    /// <remarks>
    /// Keys are segment names such as <c>total</c>, <c>securities</c> and <c>commodities</c> for the
    /// balance, margin and funds summaries, and currency codes for the market value summary.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Segments { get; }

    /// <summary>The <c>total</c> segment, covering the whole account.</summary>
    public IReadOnlyDictionary<string, string>? Total => GetSegment("total");

    /// <summary>The <c>securities</c> segment.</summary>
    public IReadOnlyDictionary<string, string>? Securities => GetSegment("securities");

    /// <summary>The <c>commodities</c> segment.</summary>
    public IReadOnlyDictionary<string, string>? Commodities => GetSegment("commodities");

    /// <summary>Returns a segment, or <see langword="null"/> when it is absent.</summary>
    /// <param name="segment">The segment name, matched case-insensitively.</param>
    public IReadOnlyDictionary<string, string>? GetSegment(string segment)
    {
        foreach (var pair in Segments)
        {
            if (string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>Reads one display-formatted value as a number and currency.</summary>
    /// <param name="segment">The segment name, for example <c>total</c> or <c>USD</c>.</param>
    /// <param name="key">The value's key, for example <c>net_liquidation</c>.</param>
    /// <returns>
    /// The parsed value, or <see langword="null"/> when the key is absent or holds one of IBKR's
    /// non-numeric placeholders such as <c>"n/a"</c> or <c>"Unlimited"</c>.
    /// </returns>
    public MonetaryValue? GetAmount(string segment, string key) =>
        GetSegment(segment) is { } values &&
        values.TryGetValue(key, out var raw) &&
        MonetaryValue.TryParse(raw, out var parsed)
            ? parsed
            : null;
}
