using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Alerts;

/// <summary>
/// One alert as it appears in the account's listing, from
/// <c>GET /iserver/account/{accountId}/alerts</c>.
/// </summary>
/// <remarks>
/// The listing carries seven fields; the alert's conditions, message and delivery settings are only
/// on <see cref="AlertDetails"/>.
/// </remarks>
public sealed record AlertSummary
{
    /// <summary>The alert's identifier.</summary>
    /// <remarks>
    /// Named <c>order_id</c> on the wire, and typed here as an <see cref="OrderId"/> rather than as
    /// an identifier of its own, because that is what it is: TWS builds an alert out of the order
    /// system, which is also why an alert has a <see cref="AlertDetails.TimeInForce"/> and an
    /// <see cref="AlertDetails.OrderStatus"/> at all. IBKR's paths call the same value
    /// <c>alertId</c>.
    /// </remarks>
    [JsonPropertyName("order_id")]
    public OrderId? Id { get; init; }

    /// <summary>The account the alert belongs to.</summary>
    [JsonPropertyName("account")]
    public AccountId? Account { get; init; }

    /// <summary>The alert's name, as shown in Trader Workstation.</summary>
    [JsonPropertyName("alert_name")]
    public string? Name { get; init; }

    /// <summary>Whether the alert is armed.</summary>
    /// <remarks>
    /// A deactivated alert still exists and still lists; it just does not fire. Change this with
    /// <c>IAlertsClient.SetActiveAsync</c>.
    /// </remarks>
    [JsonPropertyName("alert_active")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsActive { get; init; }

    /// <summary>When the alert was created.</summary>
    [JsonPropertyName("order_time")]
    [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
    public Instant? CreatedAt { get; init; }

    /// <summary>Whether the alert has fired.</summary>
    [JsonPropertyName("alert_triggered")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsTriggered { get; init; }

    /// <summary>Whether the alert fires more than once.</summary>
    /// <remarks>
    /// A non-repeatable alert deactivates itself once it has fired, so an alert that is both
    /// triggered and inactive has most likely done its job rather than been switched off.
    /// </remarks>
    [JsonPropertyName("alert_repeatable")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRepeatable { get; init; }
}

/// <summary>
/// The full description of one alert, from <c>GET /iserver/account/alert/{alertId}</c> and from
/// <c>GET /iserver/account/mta</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two endpoints return the same shape. The mobile trading assistant alert is a normal alert in
/// every respect except that it cannot be created or deleted and is the only one that populates
/// <see cref="MtaCurrency"/> and <see cref="MtaDefaults"/>.
/// </para>
/// <para>
/// Almost every field IBKR documents here as a <c>long</c> is a yes-or-no flag written as
/// <c>1</c> or <c>0</c>, and a live gateway sends some of them as real JSON booleans instead. They
/// are exposed as <see cref="bool"/> and read with <see cref="FlexibleBooleanConverter"/>, which
/// takes either.
/// </para>
/// </remarks>
public sealed record AlertDetails
{
    /// <summary>The account the alert belongs to.</summary>
    [JsonPropertyName("account")]
    public AccountId? Account { get; init; }

    /// <summary>The alert's identifier. See <see cref="AlertSummary.Id"/>.</summary>
    [JsonPropertyName("order_id")]
    public OrderId? Id { get; init; }

    /// <summary>The alert's name, as shown in Trader Workstation.</summary>
    /// <remarks>
    /// IBKR's reference names this field <c>alertName</c> and then contradicts itself: the example
    /// responses on both endpoints send <c>alert_name</c>, as does a live gateway, so that is what
    /// is bound here.
    /// </remarks>
    [JsonPropertyName("alert_name")]
    public string? Name { get; init; }

    /// <summary>The alert's time in force, for example <c>GTC</c>.</summary>
    /// <remarks>An alert has one because TWS models it as an order. See <see cref="AlertSummary.Id"/>.</remarks>
    [JsonPropertyName("tif")]
    public string? TimeInForce { get; init; }

    /// <summary>When a good-till-date alert expires.</summary>
    /// <remarks>
    /// Only populated for a good-till-date alert; a <c>GTC</c> alert leaves it null. Read as
    /// <c>YYYYMMDD-hh:mm:ss</c> in UTC, the same encoding as <see cref="AlertSummary.CreatedAt"/>,
    /// which IBKR documents only as "the UTC formatted date".
    /// </remarks>
    [JsonPropertyName("expire_time")]
    [JsonConverter(typeof(IbkrUtcDateTimeConverter))]
    public Instant? ExpiresAt { get; init; }

    /// <summary>Whether the alert is armed.</summary>
    [JsonPropertyName("alert_active")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsActive { get; init; }

    /// <summary>Whether the alert fires more than once.</summary>
    [JsonPropertyName("alert_repeatable")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRepeatable { get; init; }

    /// <summary>The address the alert emails when it fires.</summary>
    [JsonPropertyName("alert_email")]
    public string? Email { get; init; }

    /// <summary>Whether the alert sends an email when it fires.</summary>
    [JsonPropertyName("alert_send_message")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? SendsEmail { get; init; }

    /// <summary>The body the alert delivers when it fires.</summary>
    [JsonPropertyName("alert_message")]
    public string? Message { get; init; }

    /// <summary>Whether the alert raises a Trader Workstation pop-up when it fires.</summary>
    [JsonPropertyName("alert_show_popup")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? ShowsPopup { get; init; }

    /// <summary>Whether the alert plays a sound when it fires.</summary>
    [JsonPropertyName("alert_play_audio")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? PlaysAudio { get; init; }

    /// <summary>The alert's order status.</summary>
    /// <remarks>
    /// Left as text rather than modelled as an enumeration. IBKR documents the value twice over as a
    /// closed set -- "Always returns 'Presubmitted'", with <c>Presubmitted</c> and <c>Submitted</c>
    /// as the allowed values -- and a live gateway answers <c>Inactive</c>, which is neither.
    /// </remarks>
    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; init; }

    /// <summary>Whether the alert has fired.</summary>
    [JsonPropertyName("alert_triggered")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsTriggered { get; init; }

    /// <summary>The alert's foreground colour in Trader Workstation, as <c>#RRGGBB</c>.</summary>
    /// <remarks>Presentation only; IBKR notes it is not applicable to the API.</remarks>
    [JsonPropertyName("fg_color")]
    public string? ForegroundColor { get; init; }

    /// <summary>The alert's background colour in Trader Workstation, as <c>#RRGGBB</c>.</summary>
    /// <remarks>Presentation only; IBKR notes it is not applicable to the API.</remarks>
    [JsonPropertyName("bg_color")]
    public string? BackgroundColor { get; init; }

    /// <summary>Whether the alert can no longer be edited.</summary>
    [JsonPropertyName("order_not_editable")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsReadOnly { get; init; }

    /// <summary>Whether the alert delivers to mobile.</summary>
    /// <remarks>
    /// IBKR's field name is <c>itws_orders_only</c> and its description is "whether or not the alert
    /// will trigger mobile notifications". The two do not obviously say the same thing, and the
    /// reference offers nothing further; the description is what the property is named after.
    /// </remarks>
    [JsonPropertyName("itws_orders_only")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? SendsMobileNotifications { get; init; }

    /// <summary>The currency the mobile trading assistant alert reports its thresholds in.</summary>
    /// <remarks>Only set on the MTA alert. IBKR notes it applies to condition types 8 and 9.</remarks>
    [JsonPropertyName("alert_mta_currency")]
    public string? MtaCurrency { get; init; }

    /// <summary>The mobile trading assistant alert's threshold defaults, in IBKR's own encoding.</summary>
    /// <remarks>
    /// Left as text. The value is a pipe-separated list of records, each a condition type and a
    /// comma-separated set of settings --
    /// <c>9:STATE=0,MIN=-15000,MAX=15000,STEP=500,DEF_MIN=-1500,DEF_MAX=1500|8:STATE=0,...</c> --
    /// and IBKR documents neither the grammar nor the keys, so parsing it here would be guesswork
    /// presented as a type. Only the MTA alert populates it.
    /// </remarks>
    [JsonPropertyName("alert_mta_defaults")]
    public string? MtaDefaults { get; init; }

    /// <summary>The mobile trading assistant alert's fixed tracking identifier.</summary>
    /// <remarks>
    /// Null on a standard alert, and the only stable way to refer to the MTA alert. IBKR describes
    /// <see cref="Id"/> as being reissued when the alert is modified; in practice a live gateway
    /// hands out a new one on every read -- three consecutive reads returned 487543692, 487543693
    /// and 487543694 -- so it is drawn from the account's order sequence per request rather than
    /// being an identity. This one does not move.
    /// </remarks>
    [JsonPropertyName("tool_id")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? ToolId { get; init; }

    /// <summary>The alert's time zone, where it has one.</summary>
    /// <remarks>
    /// Text rather than a <see cref="DateTimeZone"/>, unlike
    /// <see cref="AlertCondition.TimeZone"/>. IBKR's own example response for the MTA alert puts the
    /// literal string "all timezones can be here" in this field, so it cannot be resolved eagerly
    /// without turning a response IBKR itself publishes into a deserialization failure.
    /// </remarks>
    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; init; }

    /// <summary>The account's configured default alert type, set in Client Portal.</summary>
    [JsonPropertyName("alert_default_type")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? DefaultType { get; init; }

    /// <summary>How many conditions the alert has.</summary>
    /// <remarks>
    /// IBKR reports the count separately from the array. They agree on every response seen so far;
    /// prefer <see cref="Conditions"/>.Count where the two could differ.
    /// </remarks>
    [JsonPropertyName("condition_size")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? ConditionCount { get; init; }

    /// <summary>Whether the alert can fire outside regular trading hours.</summary>
    [JsonPropertyName("condition_outside_rth")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? TriggersOutsideRegularTradingHours { get; init; }

    /// <summary>The conditions that arm the alert.</summary>
    [JsonPropertyName("conditions")]
    public IReadOnlyList<AlertCondition> Conditions { get; init; } = [];

    /// <summary>IBKR's error text, present instead of the alert when the identifier is unknown.</summary>
    /// <remarks>
    /// This endpoint answers an unknown alert identifier with <c>200 OK</c> and a body of
    /// <c>{"error": "Alert with order ID=1 not found."}</c> -- not a <c>404</c>, and not an empty
    /// object, so nothing about the transport says the read failed.
    /// <c>IAlertsClient.GetDetailsAsync</c> checks for it and throws, and so this is always null on
    /// anything that method returns; it is modelled because it is what the endpoint sends, and
    /// because a caller reaching the endpoint through <c>IIbkrApiClient</c> needs to see it.
    /// </remarks>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// One condition on an alert. The alert fires when its conditions are met.
/// </summary>
public sealed record AlertCondition
{
    /// <summary>The kind of condition, as IBKR's numeric code.</summary>
    /// <remarks>
    /// IBKR documents this only as "the type of condition set" and never lists the codes. Its own
    /// examples show <c>1</c> for a price condition and <c>5</c> for the mobile trading assistant's
    /// catch-all, and the note on <see cref="AlertDetails.MtaCurrency"/> implies <c>8</c> and
    /// <c>9</c> are the MTA's currency-denominated types. Nothing else about the set is documented,
    /// so it is carried through as a number rather than named.
    /// </remarks>
    [JsonPropertyName("condition_type")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? ConditionType { get; init; }

    /// <summary>The instrument the condition watches, as <c>conid@exchange</c>.</summary>
    /// <remarks>
    /// For example <c>8314@NYSE</c>. The mobile trading assistant alert uses the wildcard
    /// <c>*@*</c>, which is why this is not split into a <see cref="ConId"/> and an exchange.
    /// </remarks>
    [JsonPropertyName("conidex")]
    public string? Conidex { get; init; }

    /// <summary>The instrument's description, for example <c>IBM</c>.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription { get; init; }

    /// <summary>The comparison the condition makes, for example <c>&gt;=</c>.</summary>
    [JsonPropertyName("condition_operator")]
    public string? Operator { get; init; }

    /// <summary>The trigger method the condition uses.</summary>
    /// <remarks>Undocumented beyond "TriggerMethod value set", so carried through as a number.</remarks>
    [JsonPropertyName("condition_trigger_method")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? TriggerMethod { get; init; }

    /// <summary>The value the condition compares against.</summary>
    /// <remarks>
    /// Text, because it is not always a number: a price condition sends <c>"500.00"</c> and the
    /// mobile trading assistant's condition sends the wildcard <c>"*"</c>.
    /// </remarks>
    [JsonPropertyName("condition_value")]
    public string? Value { get; init; }

    /// <summary>Whether this condition is bound to the next one by "and" rather than "or".</summary>
    [JsonPropertyName("condition_logic_bind")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? LogicBind { get; init; }

    /// <summary>The zone a time-based condition is expressed in.</summary>
    /// <remarks>
    /// Resolved against TZDB, unlike <see cref="AlertDetails.TimeZone"/>, which IBKR's own example
    /// fills with prose. Only a time condition sets it.
    /// </remarks>
    [JsonPropertyName("condition_time_zone")]
    [JsonConverter(typeof(DateTimeZoneConverter))]
    public DateTimeZone? TimeZone { get; init; }
}

/// <summary>
/// The acknowledgement returned by the alert write endpoints -- activation and deletion.
/// </summary>
/// <remarks>
/// A successful call means IBKR accepted the request, not that it has taken effect:
/// <see cref="Text"/> is "Request was submitted". Read the alert back to confirm.
/// </remarks>
public sealed record AlertActionResult
{
    /// <summary>IBKR's request identifier. Documented as "not applicable".</summary>
    [JsonPropertyName("request_id")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? RequestId { get; init; }

    /// <summary>The identifier of the alert the request addressed.</summary>
    [JsonPropertyName("order_id")]
    public OrderId? Id { get; init; }

    /// <summary>Whether IBKR accepted the request.</summary>
    /// <remarks>
    /// Reported rather than thrown on: the call returns <c>200</c> either way, and a false result
    /// names the alerts it could not act on in <see cref="FailureList"/>.
    /// </remarks>
    [JsonPropertyName("success")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Success { get; init; }

    /// <summary>IBKR's message, for example <c>Request was submitted</c>.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The alert identifiers the request failed for, when <see cref="Success"/> is false.</summary>
    [JsonPropertyName("failure_list")]
    public string? FailureList { get; init; }
}
