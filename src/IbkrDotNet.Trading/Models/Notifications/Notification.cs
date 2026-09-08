using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Notifications;

/// <summary>
/// One notification, from <c>GET /fyi/notifications</c>.
/// </summary>
/// <remarks>
/// Every field in this group is named with one or two capital letters and nothing else -- <c>R</c>,
/// <c>D</c>, <c>MS</c>, <c>MD</c> -- so the wire names are kept in the attributes and the properties
/// say what each one holds.
/// </remarks>
public sealed record Notification
{
    /// <summary>The identifier, used to mark the notification read.</summary>
    [JsonPropertyName("ID")]
    public string? Id { get; init; }

    /// <summary>Whether the notification has been read.</summary>
    /// <remarks>
    /// IBKR documents this as the string <c>"0"</c> or <c>"1"</c> and a live gateway sends the bare
    /// number, so both are accepted.
    /// </remarks>
    [JsonPropertyName("R")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRead { get; init; }

    /// <summary>When the notification was raised.</summary>
    /// <remarks>
    /// IBKR sends this as seconds since the Unix epoch, quoted and with a fractional part it never
    /// populates -- <c>"1710847062.0"</c>. The fraction is discarded; the value is whole seconds.
    /// </remarks>
    [JsonPropertyName("D")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? Date { get; init; }

    /// <summary>The title, for example <c>FYI: Changes in Analyst Ratings</c>.</summary>
    [JsonPropertyName("MS")]
    public string? Title { get; init; }

    /// <summary>
    /// The body of the notification, as an HTML fragment.
    /// </summary>
    /// <remarks>
    /// IBKR wraps the content in <c>&lt;html&gt;</c> tags and uses <c>&lt;br /&gt;</c> for line
    /// breaks. It is not sanitized here, and it is authored by IBKR rather than by a counterparty,
    /// but it is still markup: render it as such only where that is intended.
    /// </remarks>
    [JsonPropertyName("MD")]
    public string? Content { get; init; }

    /// <summary>
    /// The category the notification belongs to, matching the <c>FC</c> of a
    /// <see cref="NotificationSetting"/>.
    /// </summary>
    /// <remarks>
    /// The link between a notification and its settings entry, and the code that says whether a
    /// disclaimer for it has been accepted.
    /// </remarks>
    [JsonPropertyName("FC")]
    public NotificationTypeCode? TypeCode { get; init; }

    /// <summary>
    /// IBKR's <c>HT</c> field, carried through unread.
    /// </summary>
    /// <remarks>
    /// The reference documents the name and the type and nothing else -- its description is the
    /// string "HT" -- and a live gateway omits the field altogether. Exposed rather than dropped so
    /// a caller who meets it is not blocked, but this library makes no claim about it.
    /// </remarks>
    [JsonPropertyName("HT")]
    public long? HT { get; init; }
}

/// <summary>
/// One subscription in the notification settings, from <c>GET /fyi/settings</c>.
/// </summary>
public sealed record NotificationSetting
{
    /// <summary>The code identifying the category, used to enable or disable it.</summary>
    [JsonPropertyName("FC")]
    public NotificationTypeCode? TypeCode { get; init; }

    /// <summary>The human-readable title, for example <c>Portfolio FYIs</c>.</summary>
    [JsonPropertyName("FN")]
    public string? Name { get; init; }

    /// <summary>A sentence describing what the category covers.</summary>
    [JsonPropertyName("FD")]
    public string? Description { get; init; }

    /// <summary>
    /// Whether the subscription can be turned on and off through
    /// <c>POST /fyi/settings/{typecode}</c>.
    /// </summary>
    /// <remarks>
    /// IBKR documents this as present "only if the subscription can be disabled/enabled manually",
    /// so an absent value means the same as <see langword="false"/>: not yours to change.
    /// </remarks>
    [JsonPropertyName("A")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsChangeable { get; init; }

    /// <summary>Whether the category's disclaimer has been acknowledged.</summary>
    /// <remarks>
    /// <para>
    /// IBKR describes <c>H</c> as "disclaimer if the notification was read", which can be read as
    /// either "a disclaimer exists" or "the disclaimer was accepted". A live gateway settles it:
    /// <c>M8</c> comes back with <c>H</c> unset and yet
    /// <c>GET /fyi/disclaimer/M8</c> returns a disclaimer, so the flag cannot mean that one exists.
    /// It was set only on the two categories the account actually receives notifications for.
    /// </para>
    /// <para>
    /// Read the disclaimer itself rather than inferring its text from this flag.
    /// </para>
    /// </remarks>
    [JsonPropertyName("H")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsDisclaimerAcknowledged { get; init; }

    /// <summary>
    /// IBKR's <c>SS</c> field, carried through unread.
    /// </summary>
    /// <remarks>
    /// Undocumented: the reference does not list it at all. A live gateway sends it on a minority of
    /// categories, always as the string <c>TA_FYI</c>, which suggests a grouping rather than a
    /// per-category value. Exposed rather than dropped, with no claim about what it means.
    /// </remarks>
    [JsonPropertyName("SS")]
    public string? SS { get; init; }
}

/// <summary>
/// The disclaimer attached to a notification category, from <c>GET /fyi/disclaimer/{typecode}</c>.
/// </summary>
public sealed record NotificationDisclaimer
{
    /// <summary>The category the disclaimer belongs to.</summary>
    [JsonPropertyName("FC")]
    public NotificationTypeCode? TypeCode { get; init; }

    /// <summary>The disclaimer text.</summary>
    [JsonPropertyName("DT")]
    public string? Text { get; init; }
}

/// <summary>
/// Where notifications are delivered, from <c>GET /fyi/deliveryoptions</c>.
/// </summary>
public sealed record NotificationDeliveryOptions
{
    /// <summary>The registered devices and whether each is enabled.</summary>
    [JsonPropertyName("E")]
    public IReadOnlyList<NotificationDevice> Devices { get; init; } = [];

    /// <summary>Whether the account's primary email address receives notifications.</summary>
    [JsonPropertyName("M")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsEmailEnabled { get; init; }
}

/// <summary>One device registered to receive notifications.</summary>
public sealed record NotificationDevice
{
    /// <summary>The device's display name, for example <c>iPhone</c>.</summary>
    [JsonPropertyName("NM")]
    public string? Name { get; init; }

    /// <summary>
    /// The device identifier, the value <c>DELETE /fyi/deliveryoptions/{deviceId}</c> takes.
    /// </summary>
    /// <remarks>
    /// A push-notification address such as <c>apn://mtws@...</c>, not an opaque number. It contains
    /// a token that identifies the installation, so treat it as a credential rather than a label.
    /// </remarks>
    [JsonPropertyName("I")]
    public string? Id { get; init; }

    /// <summary>IBKR's unique device identifier.</summary>
    /// <remarks>
    /// Documented as distinct from <see cref="Id"/>, though IBKR's own example shows the two
    /// carrying the same value.
    /// </remarks>
    [JsonPropertyName("UI")]
    public string? UniqueId { get; init; }

    /// <summary>Whether the device currently receives notifications.</summary>
    [JsonPropertyName("A")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsEnabled { get; init; }
}

/// <summary>
/// IBKR's acknowledgement of a settings change.
/// </summary>
/// <remarks>
/// The shape returned by every write in this group: marking a disclaimer read, toggling a category,
/// a device or email delivery. It confirms only that the request was accepted, and carries nothing
/// about the resulting state -- read that back if it matters.
/// </remarks>
public sealed record NotificationAcknowledgement
{
    /// <summary>Whether IBKR acknowledged the change.</summary>
    [JsonPropertyName("V")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsAcknowledged { get; init; }

    /// <summary>How long IBKR took to apply the change.</summary>
    [JsonPropertyName("T")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? Elapsed { get; init; }
}

/// <summary>
/// The result of <c>PUT /fyi/notifications/{notificationId}</c>.
/// </summary>
/// <remarks>
/// The same acknowledgement every other write in this group returns, plus the notification's
/// resulting read state -- the one write here that reports what it did. Repeated rather than
/// inherited from <see cref="NotificationAcknowledgement"/>, so that both stay sealed records
/// describing one payload each.
/// </remarks>
public sealed record NotificationReadResult
{
    /// <summary>Whether IBKR acknowledged the change.</summary>
    [JsonPropertyName("V")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsAcknowledged { get; init; }

    /// <summary>How long IBKR took to apply the change.</summary>
    [JsonPropertyName("T")]
    [JsonConverter(typeof(DurationMillisecondsConverter))]
    public Duration? Elapsed { get; init; }

    /// <summary>The notification's state after the change.</summary>
    [JsonPropertyName("P")]
    public NotificationReadState? State { get; init; }
}

/// <summary>A notification's read state, as reported after marking it.</summary>
public sealed record NotificationReadState
{
    /// <summary>The notification identifier.</summary>
    [JsonPropertyName("ID")]
    public string? Id { get; init; }

    /// <summary>Whether the notification is now marked read.</summary>
    [JsonPropertyName("R")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRead { get; init; }
}

/// <summary>The envelope <c>GET /fyi/unreadnumber</c> wraps its count in.</summary>
/// <remarks>
/// Not part of the public surface: the client returns the count itself.
/// </remarks>
internal sealed record UnreadNotificationCountResponse
{
    [JsonPropertyName("BN")]
    public long? Count { get; init; }
}

/// <summary>The body of <c>POST /fyi/settings/{typecode}</c>.</summary>
/// <param name="Enabled">Whether the category should raise notifications.</param>
internal sealed record ModifyNotificationSettingRequest(
    [property: JsonPropertyName("enabled")] bool Enabled);

/// <summary>The body of <c>POST /fyi/deliveryoptions/device</c>.</summary>
/// <param name="DeviceName">The device's display name.</param>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="UiName">IBKR's unique device identifier.</param>
/// <param name="Enabled">Whether the device should receive notifications.</param>
internal sealed record ModifyDeviceDeliveryRequest(
    [property: JsonPropertyName("deviceName")] string? DeviceName,
    [property: JsonPropertyName("deviceId")] string? DeviceId,
    [property: JsonPropertyName("uiName")] string? UiName,
    [property: JsonPropertyName("enabled")] bool Enabled);
