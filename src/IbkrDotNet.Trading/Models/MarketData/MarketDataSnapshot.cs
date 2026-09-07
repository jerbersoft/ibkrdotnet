using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.MarketData;

/// <summary>
/// A top-of-book snapshot for one instrument, from <c>GET /iserver/marketdata/snapshot</c>.
/// </summary>
/// <remarks>
/// <para>
/// IBKR keys the data points by tick identifier rather than by name — <c>"31"</c> is the last price
/// — so the values are exposed as a map with named accessors over it. See
/// <see cref="MarketDataField"/> for the identifiers.
/// </para>
/// <para>
/// Prices arrive as strings and may carry a leading letter marking their nature, such as <c>C</c>
/// for a previous close or <c>H</c> for a halt. <see cref="GetDecimal"/> strips it; read the raw
/// value through <see cref="Fields"/> when the marker matters.
/// </para>
/// </remarks>
public sealed record MarketDataSnapshot
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier and exchange, where IBKR returns one.</summary>
    [JsonPropertyName("conidEx")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>When the values were last updated.</summary>
    [JsonPropertyName("_updated")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? UpdatedAt { get; init; }

    /// <summary>IBKR's identifier for the market data server serving this stream.</summary>
    [JsonPropertyName("server_id")]
    public string? ServerId { get; init; }

    /// <summary>
    /// Every data point IBKR returned, keyed by tick identifier.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Fields { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>The last traded price.</summary>
    public decimal? LastPrice => GetDecimal(MarketDataField.LastPrice);

    /// <summary>The highest bid.</summary>
    public decimal? BidPrice => GetDecimal(MarketDataField.BidPrice);

    /// <summary>The lowest offer.</summary>
    public decimal? AskPrice => GetDecimal(MarketDataField.AskPrice);

    /// <summary>The size bid for at the bid.</summary>
    public decimal? BidSize => GetDecimal(MarketDataField.BidSize);

    /// <summary>The size offered at the ask.</summary>
    public decimal? AskSize => GetDecimal(MarketDataField.AskSize);

    /// <summary>The day's volume.</summary>
    public decimal? Volume => GetDecimal(MarketDataField.Volume);

    /// <summary>The instrument's symbol.</summary>
    public string? Symbol => GetString(MarketDataField.Symbol);

    /// <summary>
    /// The three-character market data availability marker: R realtime, D delayed, Z frozen,
    /// Y frozen delayed, N not subscribed, P snapshot, p consolidated, B top of book.
    /// </summary>
    public string? MarketDataAvailability => GetString(MarketDataField.MarketDataAvailability);

    /// <summary>Returns a data point as text, exactly as IBKR sent it.</summary>
    /// <param name="field">The tick identifier. See <see cref="MarketDataField"/>.</param>
    public string? GetString(string field)
    {
        if (!Fields.TryGetValue(field, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
    }

    /// <summary>
    /// Returns a data point as a number, discarding any leading marker letter.
    /// </summary>
    /// <param name="field">The tick identifier. See <see cref="MarketDataField"/>.</param>
    /// <returns>
    /// The value, or <see langword="null"/> when the field is absent or is not numeric.
    /// </returns>
    public decimal? GetDecimal(string field)
    {
        var text = GetString(field);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // A price may be prefixed to mark its nature, for example "C212.11" for a previous close.
        var span = text.AsSpan().Trim();
        while (span.Length > 0 && char.IsAsciiLetter(span[0]))
        {
            span = span[1..];
        }

        return decimal.TryParse(span, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}

/// <summary>The response from <c>POST /iserver/marketdata/unsubscribe</c>.</summary>
public sealed record UnsubscribeResponse
{
    /// <summary>Whether the stream was closed.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }
}

/// <summary>The response from <c>GET /iserver/marketdata/unsubscribeall</c>.</summary>
public sealed record UnsubscribeAllResponse
{
    /// <summary>Whether every stream was closed.</summary>
    [JsonPropertyName("unsubscribed")]
    public bool Unsubscribed { get; init; }
}
