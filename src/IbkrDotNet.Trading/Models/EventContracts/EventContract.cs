using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.EventContracts;

/// <summary>
/// The category tree, from <c>GET /forecast/category/tree</c>.
/// </summary>
/// <remarks>
/// The whole tree in one response -- a live gateway returns just over three hundred categories and
/// nine hundred markets -- so it is worth fetching once and holding rather than per lookup.
/// </remarks>
public sealed record EventContractCategoryTree
{
    /// <summary>
    /// Every category, keyed by its identifier such as <c>g137940</c>.
    /// </summary>
    /// <remarks>
    /// A flat map rather than a nested tree: each entry names its parent through
    /// <see cref="EventContractCategory.ParentId"/>, and the handful with no parent are the roots.
    /// The key is not repeated inside the value, so keep the pair together when passing one around.
    /// </remarks>
    [JsonPropertyName("categories")]
    public IReadOnlyDictionary<string, EventContractCategory> Categories { get; init; } =
        new Dictionary<string, EventContractCategory>();
}

/// <summary>One node of the event contract category tree.</summary>
public sealed record EventContractCategory
{
    /// <summary>The category's display name, such as <c>Illinois</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The identifier of the parent category, or <see langword="null"/> at a root.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>The markets filed directly under this category.</summary>
    /// <remarks>
    /// Empty at every level that only groups other categories. IBKR omits the field entirely on some
    /// categories and sends an empty array on others, and the two mean the same thing here.
    /// </remarks>
    [JsonPropertyName("markets")]
    public IReadOnlyList<EventContractMarketSummary> Markets { get; init; } = [];

    /// <summary>Whether the category is restricted for this account.</summary>
    /// <remarks>
    /// Undocumented: IBKR's reference lists only the name, parent and markets. A live gateway sends
    /// it on every category and every market, always <see langword="false"/> on an account with no
    /// restrictions, which is the only reading of the field this library can confirm.
    /// </remarks>
    [JsonPropertyName("is_restricted")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRestricted { get; init; }
}

/// <summary>A market as it appears in the category tree.</summary>
/// <remarks>
/// The tree is the only place a market's conid can be discovered, and
/// <see cref="ConId"/> here -- not <see cref="ProductConId"/> -- is what
/// <c>GET /forecast/contract/market</c> takes as its <c>underlyingConid</c>.
/// </remarks>
public sealed record EventContractMarketSummary
{
    /// <summary>The market's display name, such as <c>Illinois Governor General Election</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The market's symbol, such as <c>MXXIL</c>.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The exchange the market trades on, in practice always <c>FORECASTX</c>.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The market's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId? ConId { get; init; }

    /// <summary>The identifier of the product the market belongs to.</summary>
    /// <remarks>
    /// Distinct from <see cref="ConId"/> and not interchangeable with it: passing this one to
    /// <c>GET /forecast/contract/market</c> answers <c>404</c>.
    /// </remarks>
    [JsonPropertyName("product_conid")]
    public ConId? ProductConId { get; init; }

    /// <summary>Whether the market is restricted for this account.</summary>
    /// <remarks>Undocumented; see <see cref="EventContractCategory.IsRestricted"/>.</remarks>
    [JsonPropertyName("is_restricted")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRestricted { get; init; }
}

/// <summary>Which side of an event contract's question a contract pays on.</summary>
/// <remarks>
/// A real <see langword="enum"/>, unlike
/// <see cref="IbkrDotNet.Trading.Primitives.NotificationTypeCode"/>, which models a similar-looking
/// wire string as an open struct. The difference is that this set cannot grow: an event contract is
/// defined by having a yes leg and a no leg, which the API itself reports as the separate
/// <c>conid_yes</c> and <c>conid_no</c> fields. A third side would be a different product.
/// </remarks>
public enum EventContractSide
{
    /// <summary>Pays if the answer is yes. Sent as <c>Y</c>.</summary>
    [JsonStringEnumMemberName("Y")]
    Yes,

    /// <summary>Pays if the answer is no. Sent as <c>N</c>.</summary>
    [JsonStringEnumMemberName("N")]
    No,
}

/// <summary>
/// A market and the contracts written on it, from <c>GET /forecast/contract/market</c>.
/// </summary>
public sealed record EventContractMarket
{
    /// <summary>The market's display name.</summary>
    [JsonPropertyName("market_name")]
    public string? MarketName { get; init; }

    /// <summary>The exchange, either as requested or as IBKR resolved it.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The market's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The category whose logo represents this market.</summary>
    /// <remarks>
    /// A category identifier from the tree, used to pick an image. It is not necessarily the category
    /// the market is filed under.
    /// </remarks>
    [JsonPropertyName("logo_category")]
    public string? LogoCategory { get; init; }

    /// <summary>Whether IBKR considers a price chart of the underlying meaningless here.</summary>
    /// <remarks>IBKR's own description is about what a user interface should draw, not about the data.</remarks>
    [JsonPropertyName("exclude_historical_data")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? ExcludeHistoricalData { get; init; }

    /// <summary>The payout scaling ratio.</summary>
    [JsonPropertyName("payout")]
    [JsonConverter(typeof(FlexibleDecimalConverter))]
    public decimal? Payout { get; init; }

    /// <summary>The contracts written on this market, both sides of each strike.</summary>
    [JsonPropertyName("contracts")]
    public IReadOnlyList<EventContractSummary> Contracts { get; init; } = [];
}

/// <summary>One contract on an event market, as listed by the market.</summary>
public sealed record EventContractSummary
{
    /// <summary>The contract identifier, for a single side.</summary>
    [JsonPropertyName("conid")]
    public ConId? ConId { get; init; }

    /// <summary>Which side of the question this contract pays on.</summary>
    [JsonPropertyName("side")]
    public EventContractSide? Side { get; init; }

    /// <summary>The contract's expiration date.</summary>
    [JsonPropertyName("expiration")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Expiration { get; init; }

    /// <summary>The contract's strike.</summary>
    /// <remarks>
    /// IBKR documents this as an integer, and a live gateway sends <c>1.0</c> and <c>3.0</c>: on a
    /// market whose outcomes are named rather than numeric -- an election -- the strike is an ordinal
    /// standing in for a candidate, and <see cref="StrikeLabel"/> is the part worth reading. Held as
    /// a <see cref="decimal"/> so a genuinely fractional strike is not truncated.
    /// </remarks>
    [JsonPropertyName("strike")]
    [JsonConverter(typeof(FlexibleDecimalConverter))]
    public decimal? Strike { get; init; }

    /// <summary>What the strike means in words, such as <c>Above 4,700</c> or a candidate's name.</summary>
    [JsonPropertyName("strike_label")]
    public string? StrikeLabel { get; init; }

    /// <summary>The label to group this contract's expiry under, such as <c>2026</c>.</summary>
    [JsonPropertyName("expiry_label")]
    public string? ExpiryLabel { get; init; }

    /// <summary>The market this contract is written on.</summary>
    [JsonPropertyName("underlying_conid")]
    public ConId? UnderlyingConId { get; init; }

    /// <summary>The date the outcome is measured on, as IBKR formats it.</summary>
    /// <remarks>
    /// Left as text rather than parsed to a date. IBKR's own example sends <c>2025.12.31</c> and a
    /// live gateway sends <c>2026.11.3</c> -- the same separator, but zero-padded in one and not the
    /// other -- and the field is documented only as "specified date of expiration". Read
    /// <see cref="Expiration"/> when a date is what is needed.
    /// </remarks>
    [JsonPropertyName("time_specifier")]
    public string? TimeSpecifier { get; init; }

    /// <summary>The political party affiliated with this outcome, on an election market.</summary>
    /// <remarks>
    /// Undocumented. A live gateway sends it on election contracts, such as <c>Democrat</c>. Absent
    /// on markets where it would mean nothing.
    /// </remarks>
    [JsonPropertyName("party")]
    public string? Party { get; init; }
}

/// <summary>
/// The full detail of one event contract, from <c>GET /forecast/contract/details</c>.
/// </summary>
/// <remarks>
/// Addressed by a single side's conid, but describes the pair: whichever side was asked for,
/// <see cref="YesConId"/> and <see cref="NoConId"/> both come back, and <see cref="Side"/> says
/// which one was requested.
/// </remarks>
public sealed record EventContractDetails
{
    /// <summary>The identifier of the contract that pays if the answer is yes.</summary>
    [JsonPropertyName("conid_yes")]
    public ConId? YesConId { get; init; }

    /// <summary>The identifier of the contract that pays if the answer is no.</summary>
    [JsonPropertyName("conid_no")]
    public ConId? NoConId { get; init; }

    /// <summary>The question the contract settles, in words.</summary>
    [JsonPropertyName("question")]
    public string? Question { get; init; }

    /// <summary>Which side the requested conid was.</summary>
    [JsonPropertyName("side")]
    public EventContractSide? Side { get; init; }

    /// <summary>What the strike means in words.</summary>
    [JsonPropertyName("strike_label")]
    public string? StrikeLabel { get; init; }

    /// <summary>The contract's strike.</summary>
    /// <remarks>See <see cref="EventContractSummary.Strike"/> for why this is a decimal.</remarks>
    [JsonPropertyName("strike")]
    [JsonConverter(typeof(FlexibleDecimalConverter))]
    public decimal? Strike { get; init; }

    /// <summary>The exchange the contract trades on.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The contract's expiration date.</summary>
    [JsonPropertyName("expiration")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Expiration { get; init; }

    /// <summary>The contract's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The category the contract is filed under, as an identifier from the tree.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>The category whose logo represents this contract.</summary>
    [JsonPropertyName("logo_category")]
    public string? LogoCategory { get; init; }

    /// <summary>The period the outcome is measured over, as IBKR formats it, such as <c>Nov03'26</c>.</summary>
    [JsonPropertyName("measured_period")]
    public string? MeasuredPeriod { get; init; }

    /// <summary>The market the contract is written on.</summary>
    [JsonPropertyName("market_name")]
    public string? MarketName { get; init; }

    /// <summary>The immediate underlier's contract identifier.</summary>
    [JsonPropertyName("underlying_conid")]
    public ConId? UnderlyingConId { get; init; }

    /// <summary>The payout scaling ratio.</summary>
    [JsonPropertyName("payout")]
    [JsonConverter(typeof(FlexibleDecimalConverter))]
    public decimal? Payout { get; init; }

    /// <summary>The identifier of the product the contract belongs to.</summary>
    /// <remarks>Undocumented; a live gateway sends it. See <see cref="EventContractMarketSummary.ProductConId"/>.</remarks>
    [JsonPropertyName("product_conid")]
    public ConId? ProductConId { get; init; }

    /// <summary>The political party affiliated with this outcome, on an election market.</summary>
    /// <remarks>Undocumented; see <see cref="EventContractSummary.Party"/>.</remarks>
    [JsonPropertyName("party")]
    public string? Party { get; init; }

    /// <summary>Whether the contract is restricted for this account.</summary>
    /// <remarks>Undocumented; see <see cref="EventContractCategory.IsRestricted"/>.</remarks>
    [JsonPropertyName("is_restricted")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRestricted { get; init; }
}

/// <summary>
/// How a contract settles and when, from <c>GET /forecast/contract/rules</c>.
/// </summary>
/// <remarks>
/// The terms rather than the market data: which agency publishes the number that settles the
/// contract, where to read it, and when trading stops and the payout is made.
/// </remarks>
public sealed record EventContractRules
{
    /// <summary>The product's asset class, in practice <c>OPT</c>.</summary>
    [JsonPropertyName("asset_class")]
    public string? AssetClass { get; init; }

    /// <summary>The long description of what the contract is about.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The market the contract is written on.</summary>
    [JsonPropertyName("market_name")]
    public string? MarketName { get; init; }

    /// <summary>The period the outcome is measured over, as IBKR formats it.</summary>
    [JsonPropertyName("measured_period")]
    public string? MeasuredPeriod { get; init; }

    /// <summary>The strike, or the strike's label where the outcome is not numeric.</summary>
    /// <remarks>
    /// Text, not a number: IBKR's example sends <c>"5050.0"</c> and a live election market sends
    /// <c>"JB Pritzker"</c> in the same field.
    /// </remarks>
    [JsonPropertyName("threshold")]
    public string? Threshold { get; init; }

    /// <summary>The body whose published figure settles the contract.</summary>
    [JsonPropertyName("source_agency")]
    public string? SourceAgency { get; init; }

    /// <summary>Where that body publishes the figure.</summary>
    [JsonPropertyName("data_and_resolution_link")]
    public string? DataAndResolutionLink { get; init; }

    /// <summary>When the contract stops trading.</summary>
    [JsonPropertyName("last_trade_time")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? LastTradeTime { get; init; }

    /// <summary>The product code, the same value as the contract's symbol.</summary>
    [JsonPropertyName("product_code")]
    public string? ProductCode { get; init; }

    /// <summary>Where the market's terms and conditions are published.</summary>
    [JsonPropertyName("market_rules_link")]
    public string? MarketRulesLink { get; init; }

    /// <summary>When the settling figure is released.</summary>
    [JsonPropertyName("release_time")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? ReleaseTime { get; init; }

    /// <summary>When the contract pays out.</summary>
    [JsonPropertyName("payout_time")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? PayoutTime { get; init; }

    /// <summary>The payout amount, already formatted with a currency symbol, such as <c>$1.00</c>.</summary>
    /// <remarks>
    /// Text, and not the same field as the <c>payout</c> on
    /// <see cref="EventContractDetails.Payout"/>, which is a bare scaling ratio. IBKR gave the two
    /// the same name.
    /// </remarks>
    [JsonPropertyName("payout")]
    public string? Payout { get; init; }

    /// <summary>The minimum price increment, already formatted, such as <c>$0.01</c>.</summary>
    [JsonPropertyName("price_increment")]
    public string? PriceIncrement { get; init; }

    /// <summary>The price increment at each price level, where it varies.</summary>
    /// <remarks>
    /// Undocumented: IBKR's reference lists only the single <see cref="PriceIncrement"/>. A live
    /// gateway sends this alongside it, with one entry whose lower edge is zero where the increment
    /// does not in fact vary.
    /// </remarks>
    [JsonPropertyName("price_increments")]
    public IReadOnlyList<EventContractPriceIncrement> PriceIncrements { get; init; } = [];

    /// <summary>The exchange's own time zone.</summary>
    /// <remarks>
    /// Sent as <c>US/Central</c> -- a TZDB alias rather than the canonical <c>America/Chicago</c>.
    /// Both resolve, and the zone reports whichever identifier it was asked for.
    /// </remarks>
    [JsonPropertyName("exchange_timezone")]
    [JsonConverter(typeof(DateTimeZoneConverter))]
    public DateTimeZone? ExchangeTimeZone { get; init; }
}

/// <summary>The price increment that applies from a given price upwards.</summary>
public sealed record EventContractPriceIncrement
{
    /// <summary>The price at which this increment starts applying.</summary>
    /// <remarks>Sent as text, such as <c>"0.0"</c>, unlike the increment beside it.</remarks>
    [JsonPropertyName("lower_edge")]
    public string? LowerEdge { get; init; }

    /// <summary>The increment itself, already formatted, such as <c>$0.01</c>.</summary>
    [JsonPropertyName("increment")]
    public string? Increment { get; init; }
}

/// <summary>
/// When a contract trades, from <c>GET /forecast/contract/schedules</c>.
/// </summary>
/// <remarks>
/// A weekly pattern rather than dated sessions: each entry names a day of the week and the blocks it
/// trades in, all in <see cref="TimeZone"/>.
/// </remarks>
public sealed record EventContractSchedule
{
    /// <summary>The zone every time in this schedule is expressed in.</summary>
    /// <remarks>
    /// Sent as <c>US/Central</c>, a TZDB alias for <c>America/Chicago</c>. Combine it with the
    /// session times through <c>LocalDate.At(time).InZoneLeniently(zone)</c> to get an instant.
    /// </remarks>
    [JsonPropertyName("timezone")]
    [JsonConverter(typeof(DateTimeZoneConverter))]
    public DateTimeZone? TimeZone { get; init; }

    /// <summary>The trading pattern, one entry per day of the week.</summary>
    [JsonPropertyName("trading_schedules")]
    public IReadOnlyList<EventContractTradingDay> TradingDays { get; init; } = [];
}

/// <summary>One day of an event contract's weekly trading pattern.</summary>
public sealed record EventContractTradingDay
{
    /// <summary>The day of the week this pattern applies to.</summary>
    [JsonPropertyName("day_of_week")]
    [JsonConverter(typeof(IsoDayOfWeekConverter))]
    public IsoDayOfWeek? DayOfWeek { get; init; }

    /// <summary>The blocks the contract trades in on that day.</summary>
    /// <remarks>
    /// More than one where the day has an intraday closure. A live gateway reports two blocks split
    /// by a one-minute break in the afternoon.
    /// </remarks>
    [JsonPropertyName("trading_times")]
    public IReadOnlyList<EventContractTradingSession> TradingTimes { get; init; } = [];
}

/// <summary>One continuous block of trading within a day.</summary>
public sealed record EventContractTradingSession
{
    /// <summary>When the block opens, as a wall-clock time in the schedule's zone.</summary>
    [JsonPropertyName("open")]
    [JsonConverter(typeof(IbkrClockTimeConverter))]
    public LocalTime? Open { get; init; }

    /// <summary>When the block closes, as a wall-clock time in the schedule's zone.</summary>
    /// <remarks>
    /// A block running to the end of the day closes at <c>11:59 PM</c> rather than at midnight, so
    /// treat the close as inclusive of that minute.
    /// </remarks>
    [JsonPropertyName("close")]
    [JsonConverter(typeof(IbkrClockTimeConverter))]
    public LocalTime? Close { get; init; }
}
