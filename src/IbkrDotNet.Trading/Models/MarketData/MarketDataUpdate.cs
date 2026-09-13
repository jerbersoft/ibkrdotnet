using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.MarketData;

/// <summary>
/// One message from a market data stream: the values IBKR sent for an instrument on the
/// <c>smd</c> topic.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="MarketDataSnapshot"/> — data points keyed by tick identifier, read
/// through the same named accessors — plus the <c>topic</c> that routed it and when it was read.
/// Prices carry the same marker letters, and <see cref="GetDecimal"/> strips them the same way.
/// </para>
/// <para>
/// IBKR does not promise every requested field in every message. A field absent from
/// <see cref="Fields"/> was not reported in this message, and its accessor returns
/// <see langword="null"/>; it says nothing about the field's value.
/// </para>
/// <para>
/// IBKR also sends its server identifier twice, as <c>server_id</c> and again under tick
/// identifier <c>6119</c>. The second copy stays in <see cref="Fields"/>.
/// </para>
/// </remarks>
public sealed record MarketDataUpdate
{
    /// <summary>The topic that routed the message, for example <c>smd+8314</c>.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>
    /// The contract identifier as requested, with the exchange when one was given, for example
    /// <c>8314</c> or <c>8314@ARCA</c>.
    /// </summary>
    [JsonPropertyName("conidEx")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>When IBKR updated the values.</summary>
    [JsonPropertyName("_updated")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? UpdatedAt { get; init; }

    /// <summary>IBKR's identifier for the market data server serving this stream.</summary>
    [JsonPropertyName("server_id")]
    public string? ServerId { get; init; }

    /// <summary>When the transport read the message, on the injected clock.</summary>
    [JsonIgnore]
    public Instant ReceivedAt { get; init; }

    /// <summary>
    /// Every data point IBKR sent in this message, keyed by tick identifier.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Fields { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <inheritdoc cref="MarketDataSnapshot.LastPrice" />
    public decimal? LastPrice => GetDecimal(MarketDataField.LastPrice);

    /// <inheritdoc cref="MarketDataSnapshot.BidPrice" />
    public decimal? BidPrice => GetDecimal(MarketDataField.BidPrice);

    /// <inheritdoc cref="MarketDataSnapshot.AskPrice" />
    public decimal? AskPrice => GetDecimal(MarketDataField.AskPrice);

    /// <inheritdoc cref="MarketDataSnapshot.BidSize" />
    public decimal? BidSize => GetDecimal(MarketDataField.BidSize);

    /// <inheritdoc cref="MarketDataSnapshot.AskSize" />
    public decimal? AskSize => GetDecimal(MarketDataField.AskSize);

    /// <inheritdoc cref="MarketDataSnapshot.Volume" />
    public decimal? Volume => GetDecimal(MarketDataField.Volume);

    /// <inheritdoc cref="MarketDataSnapshot.Symbol" />
    public string? Symbol => GetString(MarketDataField.Symbol);

    /// <inheritdoc cref="MarketDataSnapshot.MarketDataAvailability" />
    public string? MarketDataAvailability => GetString(MarketDataField.MarketDataAvailability);

    /// <inheritdoc cref="MarketDataSnapshot.GetString" />
    public string? GetString(string field) => MarketDataValues.GetString(Fields, field);

    /// <inheritdoc cref="MarketDataSnapshot.GetDecimal" />
    public decimal? GetDecimal(string field) => MarketDataValues.GetDecimal(Fields, field);
}
