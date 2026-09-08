using NodaTime;
using NodaTime.Text;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// The date and time text patterns used on the wire by the Interactive Brokers Web API.
/// </summary>
/// <remarks>
/// IBKR encodes time in several unrelated formats depending on the endpoint, and the same logical
/// value is often available in two of them on the same object (for example <c>trade_time</c> as
/// <c>YYYYMMDD-hh:mm:ss</c> alongside <c>trade_time_r</c> as epoch milliseconds). These patterns
/// cover the textual encodings; the numeric epoch encodings are handled by the converters in
/// <c>IbkrDotNet.Trading.Serialization.Converters</c>.
/// </remarks>
public static class IbkrTimePatterns
{
    /// <summary>
    /// <c>YYYYMMDD-hh:mm:ss</c>, always UTC. Used by <c>trade_time</c>, <c>startTime</c> and
    /// <c>chartPanStartTime</c>.
    /// </summary>
    public static readonly LocalDateTimePattern UtcDateTime =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuuMMdd'-'HH':'mm':'ss");

    /// <summary>
    /// <c>YYMMDDhhmmss</c>, always UTC. Used by <c>lastExecutionTime</c> and <c>order_time</c>.
    /// </summary>
    /// <remarks>
    /// The two-digit year is resolved into the 2000s, matching the pattern's default template value.
    /// </remarks>
    public static readonly LocalDateTimePattern CompactDateTime =
        LocalDateTimePattern.CreateWithInvariantCulture("yyMMddHHmmss");

    /// <summary>
    /// <c>yyyyMMdd</c>. Used by <c>expiry</c>, <c>maturityDate</c> and the various <c>date</c> fields.
    /// </summary>
    public static readonly LocalDatePattern Date =
        LocalDatePattern.CreateWithInvariantCulture("uuuuMMdd");

    /// <summary>
    /// <c>HHmm</c>. Used by the trading schedule's <c>openingTime</c>, <c>closingTime</c> and
    /// <c>clearingCycleEndTime</c>.
    /// </summary>
    public static readonly LocalTimePattern HourMinute =
        LocalTimePattern.CreateWithInvariantCulture("HHmm");

    /// <summary>
    /// <c>h:mm tt</c> on a twelve-hour clock, such as <c>12:00 AM</c> or <c>4:15 PM</c>. Used by the
    /// event contract trading schedule's <c>open</c> and <c>close</c>.
    /// </summary>
    /// <remarks>
    /// The only twelve-hour format on the API. Every other wall-clock time IBKR sends is
    /// <see cref="HourMinute"/>, so the two are kept apart rather than merged into one tolerant
    /// pattern: a schedule that started reporting <c>0415</c> should fail loudly rather than be read
    /// as a quarter past four in the morning.
    /// </remarks>
    public static readonly LocalTimePattern ClockTime =
        LocalTimePattern.CreateWithInvariantCulture("h':'mm tt");
}
