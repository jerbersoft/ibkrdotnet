using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Serialization;

public class ConverterTests
{
    private static readonly JsonSerializerOptions Options = IbkrJson.Options;

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    // ---- Instant: epoch seconds -------------------------------------------------------------

    private sealed record EpochSeconds
    {
        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(InstantEpochSecondsConverter))]
        public Instant Value { get; init; }
    }

    [Fact]
    public void EpochSeconds_reads_the_ledger_timestamp_from_the_documented_payload()
    {
        // From GET /portfolio/{accountId}/ledger.
        var result = Read<EpochSeconds>("""{"timestamp":1754948718}""");

        Assert.Equal(Instant.FromUnixTimeSeconds(1754948718), result.Value);
    }

    [Fact]
    public void EpochSeconds_also_accepts_a_quoted_number()
    {
        var result = Read<EpochSeconds>("""{"timestamp":"1754948718"}""");

        Assert.Equal(Instant.FromUnixTimeSeconds(1754948718), result.Value);
    }

    [Fact]
    public void EpochSeconds_accepts_a_quoted_number_carrying_a_fractional_part()
    {
        // The FYI notification date is sent as "1710847062.0". The unquoted form has always been
        // tolerated for the same reason; the quoted form had not been, and rejecting it failed the
        // whole notification rather than one field.
        var result = Read<EpochSeconds>("""{"timestamp":"1710847062.0"}""");

        Assert.Equal(Instant.FromUnixTimeSeconds(1710847062), result.Value);
    }

    [Fact]
    public void EpochSeconds_round_trips_as_a_number()
    {
        var value = new EpochSeconds { Value = Instant.FromUnixTimeSeconds(1754948718) };

        Assert.Equal("""{"timestamp":1754948718}""", Write(value));
    }

    // ---- Instant: epoch milliseconds --------------------------------------------------------

    private sealed record EpochMillis
    {
        [JsonPropertyName("trade_time_r")]
        [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
        public Instant Value { get; init; }
    }

    [Fact]
    public void EpochMilliseconds_reads_a_numeric_execution_time()
    {
        // From GET /iserver/account/trades: "trade_time_r": 1702317649000.
        var result = Read<EpochMillis>("""{"trade_time_r":1702317649000}""");

        Assert.Equal(Instant.FromUnixTimeMilliseconds(1702317649000), result.Value);
    }

    [Fact]
    public void EpochMilliseconds_reads_a_quoted_execution_time()
    {
        // From GET /iserver/account/orders: "lastExecutionTime_r": "1714061006000".
        var result = Read<EpochMillis>("""{"trade_time_r":"1714061006000"}""");

        Assert.Equal(Instant.FromUnixTimeMilliseconds(1714061006000), result.Value);
    }

    // ---- Instant: YYYYMMDD-hh:mm:ss ---------------------------------------------------------

    private sealed record UtcDateTime
    {
        [JsonPropertyName("trade_time")]
        [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
        public Instant Value { get; init; }
    }

    [Fact]
    public void UtcDateTime_reads_the_documented_trade_time_format()
    {
        var result = Read<UtcDateTime>("""{"trade_time":"20231211-18:00:49"}""");

        Assert.Equal(Instant.FromUtc(2023, 12, 11, 18, 0, 49), result.Value);
    }

    [Fact]
    public void UtcDateTime_agrees_with_the_epoch_variant_of_the_same_field()
    {
        // The documented payload carries both encodings of one execution; they must agree.
        var text = Read<UtcDateTime>("""{"trade_time":"20231211-18:00:49"}""");
        var epoch = Read<EpochMillis>("""{"trade_time_r":1702317649000}""");

        Assert.Equal(epoch.Value, text.Value);
    }

    [Fact]
    public void UtcDateTime_round_trips()
    {
        var value = new UtcDateTime { Value = Instant.FromUtc(2023, 12, 11, 18, 0, 49) };

        Assert.Equal("""{"trade_time":"20231211-18:00:49"}""", Write(value));
    }

    // ---- Instant: YYMMDDhhmmss --------------------------------------------------------------

    private sealed record CompactDateTime
    {
        [JsonPropertyName("lastExecutionTime")]
        [JsonConverter(typeof(IbkrCompactDateTimeConverter))]
        public Instant Value { get; init; }
    }

    [Fact]
    public void CompactDateTime_resolves_the_two_digit_year_into_the_2000s()
    {
        var result = Read<CompactDateTime>("""{"lastExecutionTime":"240425160326"}""");

        Assert.Equal(Instant.FromUtc(2024, 4, 25, 16, 3, 26), result.Value);
    }

    [Fact]
    public void CompactDateTime_agrees_with_the_epoch_variant_of_the_same_field()
    {
        // GET /iserver/account/orders returns both for the same execution.
        var compact = Read<CompactDateTime>("""{"lastExecutionTime":"240425160326"}""");
        var epoch = Read<EpochMillis>("""{"trade_time_r":"1714061006000"}""");

        Assert.Equal(epoch.Value, compact.Value);
    }

    // ---- LocalDate: yyyyMMdd ----------------------------------------------------------------

    private sealed record ExpiryDate
    {
        [JsonPropertyName("expiry")]
        [JsonConverter(typeof(IbkrLocalDateConverter))]
        public LocalDate? Value { get; init; }
    }

    [Fact]
    public void LocalDate_reads_a_quoted_expiry()
    {
        var result = Read<ExpiryDate>("""{"expiry":"20240315"}""");

        Assert.Equal(new LocalDate(2024, 3, 15), result.Value);
    }

    [Fact]
    public void LocalDate_reads_an_unquoted_expiry()
    {
        // 'expirationDate' and 'ltd' arrive as JSON numbers.
        var result = Read<ExpiryDate>("""{"expiry":20240315}""");

        Assert.Equal(new LocalDate(2024, 3, 15), result.Value);
    }

    [Fact]
    public void A_converter_for_a_value_type_still_handles_a_null_on_the_wire()
    {
        // 'expiry' is documented as nullable for non-expiring instruments.
        var result = Read<ExpiryDate>("""{"expiry":null}""");

        Assert.Null(result.Value);
    }

    // ---- LocalTime: HHmm --------------------------------------------------------------------

    private sealed record SessionTime
    {
        [JsonPropertyName("openingTime")]
        [JsonConverter(typeof(IbkrLocalTimeConverter))]
        public LocalTime Opening { get; init; }

        [JsonPropertyName("closingTime")]
        [JsonConverter(typeof(IbkrLocalTimeConverter))]
        public LocalTime Closing { get; init; }
    }

    [Fact]
    public void LocalTime_reads_the_documented_schedule_times_including_a_leading_zero()
    {
        var result = Read<SessionTime>("""{"openingTime":"0035","closingTime":"2330"}""");

        Assert.Equal(new LocalTime(0, 35), result.Opening);
        Assert.Equal(new LocalTime(23, 30), result.Closing);
    }

    [Fact]
    public void LocalTime_round_trips_with_the_leading_zero_intact()
    {
        var value = new SessionTime { Opening = new LocalTime(0, 35), Closing = new LocalTime(23, 30) };

        Assert.Equal("""{"openingTime":"0035","closingTime":"2330"}""", Write(value));
    }

    // ---- DateTimeZone -----------------------------------------------------------------------

    private sealed record Venue
    {
        [JsonPropertyName("timezone")]
        [JsonConverter(typeof(DateTimeZoneConverter))]
        public DateTimeZone? Zone { get; init; }
    }

    [Fact]
    public void DateTimeZone_resolves_an_iana_identifier_against_tzdb()
    {
        var result = Read<Venue>("""{"timezone":"America/New_York"}""");

        Assert.Equal(DateTimeZoneProviders.Tzdb["America/New_York"], result.Zone);
    }

    [Fact]
    public void DateTimeZone_rejects_an_unknown_identifier_rather_than_silently_dropping_it()
    {
        var ex = Assert.Throws<IbkrSerializationException>(
            () => Read<Venue>("""{"timezone":"Mars/Olympus_Mons"}"""));

        Assert.Equal("Mars/Olympus_Mons", ex.RawValue);
    }

    // ---- Duration ---------------------------------------------------------------------------

    private sealed record Bars
    {
        [JsonPropertyName("barLength")]
        [JsonConverter(typeof(DurationSecondsConverter))]
        public Duration BarLength { get; init; }

        [JsonPropertyName("mktDataDelay")]
        [JsonConverter(typeof(DurationMillisecondsConverter))]
        public Duration Delay { get; init; }
    }

    [Fact]
    public void Duration_reads_seconds_and_milliseconds_with_their_own_units()
    {
        var result = Read<Bars>("""{"barLength":60,"mktDataDelay":1200}""");

        Assert.Equal(Duration.FromMinutes(1), result.BarLength);
        Assert.Equal(Duration.FromMilliseconds(1200), result.Delay);
    }

    // ---- Flexible primitives ----------------------------------------------------------------

    private sealed record Flags
    {
        [JsonPropertyName("liquidation_trade")]
        [JsonConverter(typeof(FlexibleBooleanConverter))]
        public bool Liquidation { get; init; }
    }

    [Theory]
    [InlineData("""{"liquidation_trade":1}""", true)]
    [InlineData("""{"liquidation_trade":0}""", false)]
    [InlineData("""{"liquidation_trade":"1"}""", true)]
    [InlineData("""{"liquidation_trade":"0"}""", false)]
    [InlineData("""{"liquidation_trade":true}""", true)]
    [InlineData("""{"liquidation_trade":false}""", false)]
    [InlineData("""{"liquidation_trade":"Y"}""", true)]
    [InlineData("""{"liquidation_trade":"N"}""", false)]
    public void FlexibleBoolean_accepts_every_encoding_ibkr_uses(string json, bool expected)
    {
        Assert.Equal(expected, Read<Flags>(json).Liquidation);
    }

    private sealed record Money
    {
        [JsonPropertyName("price")]
        [JsonConverter(typeof(FlexibleDecimalConverter))]
        public decimal Price { get; init; }
    }

    [Fact]
    public void FlexibleDecimal_reads_a_quoted_price_without_binary_rounding()
    {
        // GET /iserver/account/trades returns "price": "192.26" as a string.
        var result = Read<Money>("""{"price":"192.26"}""");

        Assert.Equal(192.26m, result.Price);
    }

    private sealed record OptionalMoney
    {
        // Deliberately carries no [JsonConverter]: the point is that an ordinary decimal? property
        // absorbs these without the model having to know which fields IBKR leaves empty.
        [JsonPropertyName("limit_price")]
        public decimal? LimitPrice { get; init; }

        [JsonPropertyName("alert_active")]
        public int? AlertActive { get; init; }
    }

    [Theory]
    [InlineData("""{"limit_price":""}""")]
    [InlineData("""{"limit_price":" "}""")]
    [InlineData("""{"limit_price":"None"}""")]
    [InlineData("""{"limit_price":"N/A"}""")]
    [InlineData("""{"limit_price":null}""")]
    public void A_nullable_number_reads_ibkrs_stand_ins_for_an_absent_value_as_null(string json)
    {
        // GET /iserver/account/order/status/{id} returns "limit_price": "" for a market order,
        // which read as a failure of the entire response until this was handled.
        Assert.Null(Read<OptionalMoney>(json).LimitPrice);
    }

    [Theory]
    [InlineData("""{"limit_price":317.56}""", 317.56)]
    [InlineData("""{"limit_price":"317.56"}""", 317.56)]
    public void A_nullable_number_still_reads_both_encodings_of_a_present_value(string json, double expected)
    {
        Assert.Equal((decimal)expected, Read<OptionalMoney>(json).LimitPrice);
    }

    [Fact]
    public void A_nullable_integer_reads_the_same_stand_ins()
    {
        Assert.Null(Read<OptionalMoney>("""{"alert_active":""}""").AlertActive);
        Assert.Equal(1, Read<OptionalMoney>("""{"alert_active":"1"}""").AlertActive);
    }

    [Fact]
    public void A_non_nullable_number_says_what_to_change_rather_than_reading_an_absent_value_as_zero()
    {
        // Reading "" as 0 would be worse than failing: a limit price of zero is a plausible number
        // and a wrong one.
        var ex = Assert.Throws<IbkrSerializationException>(() => Read<Money>("""{"price":""}"""));

        Assert.Contains("Decimal?", ex.Message, StringComparison.Ordinal);
    }

    // ---- Identifiers ------------------------------------------------------------------------

    private sealed record Contract
    {
        [JsonPropertyName("conid")]
        public ConId ConId { get; init; }
    }

    [Theory]
    [InlineData("""{"conid":265598}""")]
    [InlineData("""{"conid":"265598"}""")]
    public void ConId_accepts_both_the_numeric_and_quoted_encodings(string json)
    {
        Assert.Equal(new ConId(265598), Read<Contract>(json).ConId);
    }

    [Fact]
    public void ConId_is_always_written_as_a_number()
    {
        Assert.Equal("""{"conid":265598}""", Write(new Contract { ConId = new ConId(265598) }));
    }

    // ---- Bar size and period ----------------------------------------------------------------

    private sealed record Request
    {
        [JsonPropertyName("bar")]
        [JsonConverter(typeof(BarSizeConverter))]
        public BarSize Bar { get; init; }

        [JsonPropertyName("period")]
        [JsonConverter(typeof(HistoryPeriodConverter))]
        public HistoryPeriod Period { get; init; }
    }

    [Fact]
    public void BarSize_and_HistoryPeriod_round_trip_through_their_wire_forms()
    {
        var json = """{"bar":"5min","period":"1d"}""";

        var result = Read<Request>(json);

        Assert.Equal(BarSize.Minutes(5), result.Bar);
        Assert.Equal(HistoryPeriod.OneDay, result.Period);
        Assert.Equal(json, Write(result));
    }

    // ---- LocalTime: the twelve-hour clock ---------------------------------------------------

    private sealed record ClockTimes
    {
        [JsonPropertyName("open")]
        [JsonConverter(typeof(IbkrClockTimeConverter))]
        public LocalTime? Open { get; init; }
    }

    [Theory]
    [InlineData("12:00 AM", 0, 0)]
    [InlineData("4:15 PM", 16, 15)]
    [InlineData("11:59 PM", 23, 59)]
    [InlineData("12:00 PM", 12, 0)]
    public void ClockTime_reads_the_event_contract_schedule(string wire, int hour, int minute)
    {
        // Midnight and noon are the two the twelve-hour clock gets wrong if the pattern is careless:
        // both are written "12", and they are twelve hours apart.
        var result = Read<ClockTimes>($$"""{"open":"{{wire}}"}""");

        Assert.Equal(new LocalTime(hour, minute), result.Open);
    }

    [Fact]
    public void ClockTime_round_trips()
    {
        Assert.Equal(
            """{"open":"4:15 PM"}""",
            Write(new ClockTimes { Open = new LocalTime(16, 15) }));
    }

    [Fact]
    public void ClockTime_refuses_the_other_wall_clock_format_ibkr_uses()
    {
        // HHmm belongs to IbkrLocalTimeConverter. Accepting it here would read "0415" as a quarter
        // past four in the morning on an endpoint that never sends that format.
        Assert.Throws<IbkrSerializationException>(() => Read<ClockTimes>("""{"open":"0415"}"""));
    }

    // ---- IsoDayOfWeek -----------------------------------------------------------------------

    private sealed record ScheduleDay
    {
        [JsonPropertyName("day_of_week")]
        [JsonConverter(typeof(IsoDayOfWeekConverter))]
        public IsoDayOfWeek? Day { get; init; }
    }

    [Fact]
    public void DayOfWeek_reads_the_english_name()
    {
        Assert.Equal(IsoDayOfWeek.Saturday, Read<ScheduleDay>("""{"day_of_week":"Saturday"}""").Day);
        Assert.Equal(IsoDayOfWeek.Monday, Read<ScheduleDay>("""{"day_of_week":"Monday"}""").Day);
    }

    [Fact]
    public void DayOfWeek_treats_ibkrs_absent_markers_as_null_but_None_as_a_failure()
    {
        // "" is one of IBKR's stand-ins for absent and is absorbed like any other. The literal
        // "None" is not: it is the enum's own zero value, and reading it back would claim IBKR
        // named a day it did not.
        Assert.Null(Read<ScheduleDay>("""{"day_of_week":""}""").Day);
        Assert.Throws<IbkrSerializationException>(
            () => Read<ScheduleDay>("""{"day_of_week":"Caturday"}"""));
    }

    // ---- DateTimeZone: the alias IBKR actually sends -----------------------------------------

    private sealed record ExchangeZone
    {
        [JsonPropertyName("timezone")]
        [JsonConverter(typeof(DateTimeZoneConverter))]
        public DateTimeZone? Zone { get; init; }
    }

    [Fact]
    public void TimeZone_resolves_the_tzdb_link_the_event_contracts_send()
    {
        // Event contracts report "US/Central", a backward-compatibility link, where the rest of the
        // API sends canonical identifiers like "America/New_York". NodaTime's TZDB provider carries
        // the links, so the alias resolves and behaves identically to the canonical zone.
        var result = Read<ExchangeZone>("""{"timezone":"US/Central"}""");

        Assert.NotNull(result.Zone);
        var noon = new LocalDateTime(2026, 1, 15, 12, 0);
        Assert.Equal(
            noon.InZoneLeniently(DateTimeZoneProviders.Tzdb["America/Chicago"]).ToInstant(),
            noon.InZoneLeniently(result.Zone).ToInstant());
    }
}

public class LooseWireValueTests
{
    private static readonly JsonSerializerOptions Options = IbkrJson.Options;

    private sealed record Instrument
    {
        [JsonPropertyName("expiry")]
        [JsonConverter(typeof(IbkrLocalDateConverter))]
        public LocalDate? Expiry { get; init; }

        [JsonPropertyName("strike")]
        public string? Strike { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }

    [Theory]
    [InlineData("""{"expiry":null}""")]
    [InlineData("""{"expiry":"None"}""")]
    [InlineData("""{"expiry":""}""")]
    [InlineData("""{"expiry":"N/A"}""")]
    public void Treats_ibkrs_textual_stand_ins_for_absent_as_null(string json)
    {
        // A non-expiring instrument reports null on one endpoint and the literal "None" on another.
        Assert.Null(JsonSerializer.Deserialize<Instrument>(json, Options)!.Expiry);
    }

    [Fact]
    public void Still_reads_a_real_date_through_the_same_converter()
    {
        var result = JsonSerializer.Deserialize<Instrument>("""{"expiry":"20240315"}""", Options)!;

        Assert.Equal(new LocalDate(2024, 3, 15), result.Expiry);
    }

    [Fact]
    public void Reads_a_string_field_that_ibkr_sent_as_a_number()
    {
        // '/portfolio/{accountId}/summary' documents currency as a string and sends 1.1;
        // 'strike' is documented as a string and arrives as 0.
        var result = JsonSerializer.Deserialize<Instrument>("""{"strike":0,"currency":1.1}""", Options)!;

        Assert.Equal("0", result.Strike);
        Assert.Equal("1.1", result.Currency);
    }

    [Fact]
    public void Reads_a_string_field_that_ibkr_sent_as_a_boolean()
    {
        var result = JsonSerializer.Deserialize<Instrument>("""{"currency":true}""", Options)!;

        Assert.Equal("true", result.Currency);
    }

    private sealed record RequiredExpiry
    {
        [JsonPropertyName("expiry")]
        [JsonConverter(typeof(IbkrLocalDateConverter))]
        public LocalDate Expiry { get; init; }
    }

    [Fact]
    public void Refuses_an_absent_value_where_the_property_cannot_represent_one()
    {
        // The same converter serves LocalDate and LocalDate?; only the nullable form can absorb an
        // absent value, and the message says so rather than producing a default date.
        var ex = Assert.Throws<IbkrSerializationException>(
            () => JsonSerializer.Deserialize<RequiredExpiry>("""{"expiry":"None"}""", Options));

        Assert.Contains("LocalDate?", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_dictionary_keys_working_with_the_global_string_converter()
    {
        // Registering a converter for string globally also intercepts dictionary keys.
        var result = JsonSerializer.Deserialize<Dictionary<string, int>>("""{"AUD":1,"BASE":2}""", Options)!;

        Assert.Equal(1, result["AUD"]);
        Assert.Equal(2, result["BASE"]);
    }

}
