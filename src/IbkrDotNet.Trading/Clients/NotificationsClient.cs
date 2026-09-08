using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Notifications;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="INotificationsClient" />
public sealed class NotificationsClient(IIbkrApiClient apiClient) : INotificationsClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public async Task<long?> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SendAsync<UnreadNotificationCountResponse>(
            IbkrRequest.Get("/v1/api/fyi/unreadnumber"),
            cancellationToken).ConfigureAwait(false);

        return response.Count;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Notification>> GetAllAsync(
        long max,
        IEnumerable<NotificationTypeCode>? include = null,
        IEnumerable<NotificationTypeCode>? exclude = null,
        string? notificationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        return _apiClient.SendAsync<IReadOnlyList<Notification>>(
            IbkrRequest.Get("/v1/api/fyi/notifications")
                .WithQuery("max", max)
                .WithCommaSeparatedQuery("include", include?.Select(code => code.Value))
                .WithCommaSeparatedQuery("exclude", exclude?.Select(code => code.Value))
                .WithQuery("id", notificationId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<NotificationReadResult> MarkReadAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);

        return _apiClient.SendAsync<NotificationReadResult>(
            IbkrRequest.Put($"/v1/api/fyi/notifications/{IbkrRequest.PathSegment(notificationId)}"),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NotificationSetting>> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<NotificationSetting>>(
            IbkrRequest.Get("/v1/api/fyi/settings"),
            cancellationToken);

    /// <inheritdoc />
    public Task<NotificationAcknowledgement> SetSubscribedAsync(
        NotificationTypeCode typeCode,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<NotificationAcknowledgement>(
            IbkrRequest.Post($"/v1/api/fyi/settings/{TypeCodeSegment(typeCode)}")
                .WithJsonBody(new ModifyNotificationSettingRequest(enabled)),
            cancellationToken);

    /// <inheritdoc />
    public Task<NotificationDisclaimer> GetDisclaimerAsync(
        NotificationTypeCode typeCode,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<NotificationDisclaimer>(
            IbkrRequest.Get($"/v1/api/fyi/disclaimer/{TypeCodeSegment(typeCode)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<NotificationAcknowledgement> MarkDisclaimerReadAsync(
        NotificationTypeCode typeCode,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<NotificationAcknowledgement>(
            IbkrRequest.Put($"/v1/api/fyi/disclaimer/{TypeCodeSegment(typeCode)}"),
            cancellationToken);

    /// <inheritdoc />
    public Task<NotificationDeliveryOptions> GetDeliveryOptionsAsync(
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<NotificationDeliveryOptions>(
            IbkrRequest.Get("/v1/api/fyi/deliveryoptions"),
            cancellationToken);

    /// <inheritdoc />
    public Task<NotificationAcknowledgement> SetDeviceEnabledAsync(
        NotificationDevice device,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        return _apiClient.SendAsync<NotificationAcknowledgement>(
            IbkrRequest.Post("/v1/api/fyi/deliveryoptions/device")
                .WithJsonBody(new ModifyDeviceDeliveryRequest(
                    device.Name, device.Id, device.UniqueId, enabled)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<NotificationAcknowledgement> SetEmailEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<NotificationAcknowledgement>(
            IbkrRequest.Put("/v1/api/fyi/deliveryoptions/email").WithQuery("enabled", enabled),
            cancellationToken);

    /// <inheritdoc />
    public Task DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        // Deliberately the response-less overload. IBKR documents this one as returning an empty
        // body with 200, so asking for a payload would turn every success into a failure.
        return _apiClient.SendAsync(
            IbkrRequest.Delete($"/v1/api/fyi/deliveryoptions/{IbkrRequest.PathSegment(deviceId)}"),
            cancellationToken);
    }

    // A default-constructed NotificationTypeCode has a null Value, which would render as the empty
    // segment and silently address /fyi/settings rather than a category under it.
    private static string TypeCodeSegment(NotificationTypeCode typeCode) =>
        string.IsNullOrWhiteSpace(typeCode.Value)
            ? throw new ArgumentException(
                "The notification type code is empty; construct one with a code such as \"OE\".",
                nameof(typeCode))
            : IbkrRequest.PathSegment(typeCode.Value);
}
