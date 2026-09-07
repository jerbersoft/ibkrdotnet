using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>An instrument definition, from <c>GET /trsrv/secdef</c>.</summary>
public sealed record InstrumentDefinition
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The instrument's name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The instrument's full display name.</summary>
    [JsonPropertyName("fullName")]
    public string? FullName { get; init; }

    /// <summary>The instrument's name in Chinese.</summary>
    [JsonPropertyName("chineseName")]
    public string? ChineseName { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("assetClass")]
    public string? AssetClass { get; init; }

    /// <summary>The instrument's type, for example <c>COMMON</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The country the instrument is issued in.</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }

    /// <summary>The exchange the instrument is listed on.</summary>
    [JsonPropertyName("listingExchange")]
    public string? ListingExchange { get; init; }

    /// <summary>Every exchange the instrument trades on, comma separated.</summary>
    [JsonPropertyName("allExchanges")]
    public string? AllExchanges { get; init; }

    /// <summary>The instrument's industry sector.</summary>
    [JsonPropertyName("sector")]
    public string? Sector { get; init; }

    /// <summary>The instrument's industry sector group.</summary>
    [JsonPropertyName("sectorGroup")]
    public string? SectorGroup { get; init; }

    /// <summary>The instrument's industry sub-category.</summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>The instrument's expiry, where it has one.</summary>
    [JsonPropertyName("expiry")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Expiry { get; init; }

    /// <summary>The last day the instrument trades, where applicable.</summary>
    [JsonPropertyName("lastTradingDay")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? LastTradingDay { get; init; }

    /// <summary>The option right, where applicable.</summary>
    [JsonPropertyName("putOrCall")]
    public string? PutOrCall { get; init; }

    /// <summary>The strike price, where applicable.</summary>
    [JsonPropertyName("strike")]
    public string? Strike { get; init; }

    /// <summary>The underlying instrument's contract identifier, where applicable.</summary>
    [JsonPropertyName("undConid")]
    public ConId? UnderlyingConId { get; init; }

    /// <summary>The contract multiplier, where applicable.</summary>
    [JsonPropertyName("multiplier")]
    public decimal? Multiplier { get; init; }

    /// <summary>Whether options are available on the instrument at IBKR.</summary>
    [JsonPropertyName("hasOptions")]
    public bool? HasOptions { get; init; }

    /// <summary>Whether the instrument is issued in the United States.</summary>
    [JsonPropertyName("isUS")]
    public bool? IsUnitedStates { get; init; }

    /// <summary>Whether the instrument is an event contract.</summary>
    [JsonPropertyName("isEventContract")]
    public bool? IsEventContract { get; init; }

    /// <summary>How long IBKR took to serve the definition.</summary>
    [JsonPropertyName("time")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? RetrievalTime { get; init; }

    /// <summary>Price increments used when pricing orders for the instrument.</summary>
    [JsonPropertyName("incrementRules")]
    public IReadOnlyList<IncrementRule> IncrementRules { get; init; } = [];

    /// <summary>
    /// Display increments for the instrument's market data.
    /// </summary>
    /// <remarks>
    /// A list here, though the equivalent field on a position is a single object. The shape follows
    /// IBKR rather than being normalized, so neither endpoint silently loses data.
    /// </remarks>
    [JsonPropertyName("displayRule")]
    public IReadOnlyList<DisplayRule> DisplayRules { get; init; } = [];
}
