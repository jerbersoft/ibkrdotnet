using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>
/// Instrument details, from <c>GET /iserver/contract/{conid}/info</c> and
/// <c>GET /iserver/contract/{conid}/info-and-rules</c>.
/// </summary>
/// <remarks>
/// This endpoint family names its fields in snake case, unlike most of the Trading API.
/// </remarks>
public sealed record InstrumentInfo
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("con_id")]
    public ConId ConId { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("instrument_type")]
    public string? InstrumentType { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The exchange the instrument is routed to.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>Every exchange the instrument may be routed to, comma separated.</summary>
    [JsonPropertyName("valid_exchanges")]
    public string? ValidExchanges { get; init; }

    /// <summary>The instrument's trading class.</summary>
    [JsonPropertyName("trading_class")]
    public string? TradingClass { get; init; }

    /// <summary>The instrument's local symbol.</summary>
    [JsonPropertyName("local_symbol")]
    public string? LocalSymbol { get; init; }

    /// <summary>The instrument's CFI code.</summary>
    [JsonPropertyName("cfi_code")]
    public string? CfiCode { get; init; }

    /// <summary>The instrument's CUSIP, where it has one.</summary>
    [JsonPropertyName("cusip")]
    public string? Cusip { get; init; }

    /// <summary>The industry the issuer operates in.</summary>
    [JsonPropertyName("industry")]
    public string? Industry { get; init; }

    /// <summary>The issuer's category.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>The underlying instrument's contract identifier, where applicable.</summary>
    [JsonPropertyName("underlying_con_id")]
    public ConId? UnderlyingConId { get; init; }

    /// <summary>The underlying's issuer, where applicable.</summary>
    [JsonPropertyName("underlying_issuer")]
    public string? UnderlyingIssuer { get; init; }

    /// <summary>The contract's final maturity date, where applicable.</summary>
    [JsonPropertyName("maturity_date")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? MaturityDate { get; init; }

    /// <summary>The contract's expiration month, where applicable.</summary>
    [JsonPropertyName("expiry_full")]
    public string? ExpiryFull { get; init; }

    /// <summary>The contract month, where applicable.</summary>
    [JsonPropertyName("contract_month")]
    public string? ContractMonth { get; init; }

    /// <summary>The contract multiplier, where applicable.</summary>
    [JsonPropertyName("multiplier")]
    public string? Multiplier { get; init; }

    /// <summary>Whether long positions may be sold.</summary>
    [JsonPropertyName("allow_sell_long")]
    public bool? AllowSellLong { get; init; }

    /// <summary>Whether the instrument trades commission free.</summary>
    [JsonPropertyName("is_zero_commission_security")]
    public bool? IsZeroCommissionSecurity { get; init; }

    /// <summary>Whether the instrument trades during regular trading hours only.</summary>
    [JsonPropertyName("r_t_h")]
    public bool? RegularTradingHoursOnly { get; init; }

    /// <summary>Whether SMART routing is available.</summary>
    [JsonPropertyName("smart_available")]
    public bool? SmartAvailable { get; init; }

    /// <summary>Free-text detail about the instrument.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The instrument's classifier.</summary>
    [JsonPropertyName("classifier")]
    public string? Classifier { get; init; }

    /// <summary>The contract clarification type.</summary>
    [JsonPropertyName("contract_clarification_type")]
    public string? ContractClarificationType { get; init; }

    /// <summary>
    /// The market rules, present only on <c>info-and-rules</c>.
    /// </summary>
    [JsonPropertyName("rules")]
    public ContractRules? Rules { get; init; }
}

/// <summary>
/// Instrument attributes, from <c>GET /iserver/secdef/info</c>.
/// </summary>
/// <remarks>Camel-cased, unlike the snake-cased <see cref="InstrumentInfo"/>.</remarks>
public sealed record InstrumentAttributes
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>The exchange the instrument is listed on.</summary>
    [JsonPropertyName("listingExchange")]
    public string? ListingExchange { get; init; }

    /// <summary>The exchange the instrument is routed to.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; init; }

    /// <summary>The currency the instrument trades in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>Every exchange the instrument may be routed to, comma separated.</summary>
    [JsonPropertyName("validExchanges")]
    public string? ValidExchanges { get; init; }

    /// <summary>The option or warrant right, where applicable.</summary>
    [JsonPropertyName("right")]
    public string? Right { get; init; }

    /// <summary>The strike price, where applicable.</summary>
    [JsonPropertyName("strike")]
    public string? Strike { get; init; }

    /// <summary>The contract's maturity date, where applicable.</summary>
    [JsonPropertyName("maturityDate")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? MaturityDate { get; init; }

    /// <summary>How prices should be rendered. Internal use.</summary>
    [JsonPropertyName("priceRendering")]
    public string? PriceRendering { get; init; }
}
