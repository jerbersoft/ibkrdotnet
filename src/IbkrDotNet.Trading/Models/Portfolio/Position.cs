using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Contracts;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Portfolio;

/// <summary>An account's position in one instrument.</summary>
public sealed record Position
{
    /// <summary>The account holding the position.</summary>
    [JsonPropertyName("acctId")]
    public AccountId AccountId { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The size of the position, in units of the instrument.</summary>
    [JsonPropertyName("position")]
    public decimal? Quantity { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("assetClass")]
    public string? AssetClass { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>A human-readable description of the instrument.</summary>
    [JsonPropertyName("contractDesc")]
    public string? ContractDescription { get; init; }

    /// <summary>The instrument's full display name.</summary>
    [JsonPropertyName("fullName")]
    public string? FullName { get; init; }

    /// <summary>The formal name of the entity or asset the instrument relates to.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The average cost of the position.</summary>
    [JsonPropertyName("avgCost")]
    public decimal? AverageCost { get; init; }

    /// <summary>The average price of the position.</summary>
    [JsonPropertyName("avgPrice")]
    public decimal? AveragePrice { get; init; }

    /// <summary>The current market price, in the instrument's currency.</summary>
    [JsonPropertyName("mktPrice")]
    public decimal? MarketPrice { get; init; }

    /// <summary>The current market value, in the instrument's currency.</summary>
    [JsonPropertyName("mktValue")]
    public decimal? MarketValue { get; init; }

    /// <summary>Realized profit and loss, in the instrument's currency.</summary>
    [JsonPropertyName("realizedPnl")]
    public decimal? RealizedPnl { get; init; }

    /// <summary>Unrealized profit and loss, in the instrument's currency.</summary>
    [JsonPropertyName("unrealizedPnl")]
    public decimal? UnrealizedPnl { get; init; }

    /// <summary>The average cost in the account's base currency.</summary>
    [JsonPropertyName("baseAvgCost")]
    public decimal? BaseAverageCost { get; init; }

    /// <summary>The average price in the account's base currency.</summary>
    [JsonPropertyName("baseAvgPrice")]
    public decimal? BaseAveragePrice { get; init; }

    /// <summary>The market price in the account's base currency.</summary>
    [JsonPropertyName("baseMktPrice")]
    public decimal? BaseMarketPrice { get; init; }

    /// <summary>The market value in the account's base currency.</summary>
    [JsonPropertyName("baseMktValue")]
    public decimal? BaseMarketValue { get; init; }

    /// <summary>Realized profit and loss in the account's base currency.</summary>
    [JsonPropertyName("baseRealizedPnl")]
    public decimal? BaseRealizedPnl { get; init; }

    /// <summary>Unrealized profit and loss in the account's base currency.</summary>
    [JsonPropertyName("baseUnrealizedPnl")]
    public decimal? BaseUnrealizedPnl { get; init; }

    /// <summary>The exchange the instrument is listed on.</summary>
    [JsonPropertyName("listingExchange")]
    public string? ListingExchange { get; init; }

    /// <summary>Every exchange the instrument trades on, comma separated.</summary>
    [JsonPropertyName("allExchanges")]
    public string? AllExchanges { get; init; }

    /// <summary>The country the instrument is issued in.</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }

    /// <summary>The instrument's industry sector.</summary>
    [JsonPropertyName("sector")]
    public string? Sector { get; init; }

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

    /// <summary>The option strike price, where applicable. IBKR returns this as a string.</summary>
    [JsonPropertyName("strike")]
    public string? Strike { get; init; }

    /// <summary>The option right, where applicable.</summary>
    [JsonPropertyName("putOrCall")]
    public string? PutOrCall { get; init; }

    /// <summary>The option exercise style, where applicable.</summary>
    [JsonPropertyName("exerciseStyle")]
    public string? ExerciseStyle { get; init; }

    /// <summary>The instrument's multiplier, where applicable.</summary>
    [JsonPropertyName("multiplier")]
    public decimal? Multiplier { get; init; }

    /// <summary>The underlying instrument's contract identifier, where applicable.</summary>
    [JsonPropertyName("undConid")]
    public ConId? UnderlyingConId { get; init; }

    /// <summary>Whether options are available on the instrument at IBKR.</summary>
    [JsonPropertyName("hasOptions")]
    public bool? HasOptions { get; init; }

    /// <summary>Whether the instrument is issued in the United States.</summary>
    [JsonPropertyName("isUS")]
    public bool? IsUnitedStates { get; init; }

    /// <summary>Whether the instrument is an event contract.</summary>
    [JsonPropertyName("isEventContract")]
    public bool? IsEventContract { get; init; }

    /// <summary>The model portfolio contributing this position, where there is one.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Display increments for the instrument's market data.</summary>
    [JsonPropertyName("displayRule")]
    public DisplayRule? DisplayRule { get; init; }

    /// <summary>Price increments used when pricing orders for the instrument.</summary>
    [JsonPropertyName("incrementRules")]
    public IReadOnlyList<IncrementRule> IncrementRules { get; init; } = [];

    /// <summary>How long IBKR took to retrieve the position data.</summary>
    [JsonPropertyName("time")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? RetrievalTime { get; init; }

    /// <summary>The maximum number of accounts returnable in one request.</summary>
    [JsonPropertyName("pageSize")]
    public long? PageSize { get; init; }
}
