using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>
/// A venue's trading schedule, from <c>GET /trsrv/secdef/schedule</c>.
/// </summary>
/// <remarks>
/// Every time in this response is a wall-clock time in <see cref="TimeZone"/>, not an instant. Pair
/// them with a <see cref="LocalDate"/> and the zone to get an absolute moment.
/// </remarks>
public sealed record TradingSchedule
{
    /// <summary>The schedule identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The trade venue identifier.</summary>
    [JsonPropertyName("tradeVenueId")]
    public string? TradeVenueId { get; init; }

    /// <summary>The exchange, for example <c>NYSE</c>.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>A description of the exchange.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The time zone the schedule's times are expressed in, resolved against the TZDB.
    /// </summary>
    [JsonPropertyName("timezone")]
    [JsonConverter(typeof(DateTimeZoneConverter))]
    public DateTimeZone? TimeZone { get; init; }

    /// <summary>The schedule entries.</summary>
    [JsonPropertyName("schedules")]
    public IReadOnlyList<TradingScheduleEntry> Schedules { get; init; } = [];
}

/// <summary>One entry of a <see cref="TradingSchedule"/>.</summary>
public sealed record TradingScheduleEntry
{
    /// <summary>
    /// The date this entry applies to, which may be a specific date or every occurrence of a
    /// weekday.
    /// </summary>
    [JsonPropertyName("tradingScheduleDate")]
    public TradingScheduleDate? Date { get; init; }

    /// <summary>When the clearing cycle ends.</summary>
    [JsonPropertyName("clearingCycleEndTime")]
    [JsonConverter(typeof(IbkrLocalTimeConverter))]
    public LocalTime? ClearingCycleEndTime { get; init; }

    /// <summary>
    /// The regular trading sessions. A separate entry appears when liquid hours differ from the full
    /// trading day.
    /// </summary>
    [JsonPropertyName("sessions")]
    public IReadOnlyList<TradingSession> Sessions { get; init; } = [];

    /// <summary>The full trading day's hours.</summary>
    [JsonPropertyName("tradingtimes")]
    public IReadOnlyList<TradingSession> TradingTimes { get; init; } = [];
}

/// <summary>A period during which an instrument trades, in the venue's local time.</summary>
public sealed record TradingSession
{
    /// <summary>The session's opening time, in the venue's time zone.</summary>
    [JsonPropertyName("openingTime")]
    [JsonConverter(typeof(IbkrLocalTimeConverter))]
    public LocalTime? OpeningTime { get; init; }

    /// <summary>The session's closing time, in the venue's time zone.</summary>
    [JsonPropertyName("closingTime")]
    [JsonConverter(typeof(IbkrLocalTimeConverter))]
    public LocalTime? ClosingTime { get; init; }

    /// <summary>
    /// <c>LIQUID</c> when the whole trading day is considered liquid.
    /// </summary>
    [JsonPropertyName("prop")]
    public string? Property { get; init; }

    /// <summary>Whether day orders are cancelled at the close of this session.</summary>
    [JsonPropertyName("cancelDayOrders")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? CancelsDayOrders { get; init; }
}
