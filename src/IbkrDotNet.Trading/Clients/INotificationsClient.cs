using IbkrDotNet.Trading.Models.Notifications;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The FYIs and Notifications endpoints, covering the messages IBKR raises about an account and the
/// subscriptions that control them.
/// </summary>
/// <remarks>
/// <para>
/// Notifications belong to the username rather than to an account, and are the same messages Trader
/// Workstation, Client Portal and the IBKR mobile apps show. Everything on this client except the
/// four reads changes settings the user sees in those applications, and none of those changes
/// undoes itself.
/// </para>
/// <para>
/// Every endpoint in the group is limited to one request per second, which the client enforces.
/// </para>
/// </remarks>
public interface INotificationsClient
{
    /// <summary>
    /// Returns how many notifications are unread.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The number of unread notifications, or <see langword="null"/> when IBKR returns no count.
    /// </returns>
    /// <remarks>
    /// The count is nullable because IBKR documents its <c>BN</c> field as optional, and an absent
    /// count is not the same claim as a count of zero.
    /// </remarks>
    Task<long?> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists notifications, newest first.
    /// </summary>
    /// <param name="max">The greatest number of notifications to return. IBKR requires it.</param>
    /// <param name="include">
    /// Categories to restrict the listing to, or <see langword="null"/> for all of them.
    /// </param>
    /// <param name="exclude">Categories to leave out of the listing.</param>
    /// <param name="notificationId">A single notification to return, by identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is not positive.</exception>
    /// <remarks>
    /// IBKR types <c>include</c> and <c>exclude</c> as "any" and does not say what they match. The
    /// category code is the only field a notification carries that a filter could select on, so
    /// that is what this takes; if IBKR means something else by them, the parameters are wrong
    /// rather than the listing.
    /// </remarks>
    Task<IReadOnlyList<Notification>> GetAllAsync(
        long max,
        IEnumerable<NotificationTypeCode>? include = null,
        IEnumerable<NotificationTypeCode>? exclude = null,
        string? notificationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification read.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException"><paramref name="notificationId"/> is empty.</exception>
    /// <remarks>
    /// IBKR describes the endpoint as marking a message "read or unread" but documents no way to say
    /// which, and its response reports the state that resulted. Read
    /// <see cref="NotificationReadResult.State"/> rather than assuming the notification is now read.
    /// </remarks>
    Task<NotificationReadResult> MarkReadAsync(
        string notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the notification categories and whether the username is subscribed to each.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<NotificationSetting>> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to or unsubscribes from a category of notification.
    /// </summary>
    /// <param name="typeCode">The category.</param>
    /// <param name="enabled">Whether the category should raise notifications.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// Only categories whose <see cref="NotificationSetting.IsChangeable"/> is set can be changed.
    /// The change persists against the username and is visible in every IBKR application.
    /// </remarks>
    Task<NotificationAcknowledgement> SetSubscribedAsync(
        NotificationTypeCode typeCode,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the disclaimer attached to a category of notification.
    /// </summary>
    /// <param name="typeCode">The category.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<NotificationDisclaimer> GetDisclaimerAsync(
        NotificationTypeCode typeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a category's disclaimer as read.
    /// </summary>
    /// <param name="typeCode">The category.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// This records the user's acknowledgement of a legal notice against their username, and IBKR
    /// documents no way to withdraw it. Call it on the user's behalf only when they have actually
    /// seen the text <see cref="GetDisclaimerAsync"/> returns.
    /// </remarks>
    Task<NotificationAcknowledgement> MarkDisclaimerReadAsync(
        NotificationTypeCode typeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads where notifications are delivered: the registered devices, and whether email is on.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<NotificationDeliveryOptions> GetDeliveryOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns delivery to a registered device on or off.
    /// </summary>
    /// <param name="device">
    /// The device, as returned by <see cref="GetDeliveryOptionsAsync"/>. IBKR documents every field
    /// of the request as optional and does not say which one it matches on, so the whole device is
    /// sent back.
    /// </param>
    /// <param name="enabled">Whether the device should receive notifications.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    Task<NotificationAcknowledgement> SetDeviceEnabledAsync(
        NotificationDevice device,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns delivery to the account's primary email address on or off.
    /// </summary>
    /// <param name="enabled">Whether email should receive notifications.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<NotificationAcknowledgement> SetEmailEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device from the list that receives notifications.
    /// </summary>
    /// <param name="deviceId">
    /// The device identifier, <see cref="NotificationDevice.Id"/> from
    /// <see cref="GetDeliveryOptionsAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException"><paramref name="deviceId"/> is empty.</exception>
    /// <remarks>
    /// Permanent as far as this API is concerned: IBKR documents no endpoint that registers a
    /// device, so a device deleted here can only be restored by the application that registered it.
    /// The endpoint returns an empty body, so there is nothing to report but success.
    /// </remarks>
    Task DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
}
