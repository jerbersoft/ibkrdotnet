using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Alerts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IAlertsClient" />
public sealed class AlertsClient(IIbkrApiClient apiClient) : IAlertsClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<IReadOnlyList<AlertSummary>> GetAllAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<IReadOnlyList<AlertSummary>>(
            IbkrRequest.Get(
                $"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/alerts"),
            cancellationToken);

    /// <inheritdoc />
    public async Task<AlertDetails> GetDetailsAsync(
        OrderId alertId,
        CancellationToken cancellationToken = default)
    {
        var path = $"/v1/api/iserver/account/alert/{IbkrRequest.PathSegment(alertId.Value)}";

        // 'type' is a required query parameter with exactly one allowed value, so it is supplied
        // here rather than asked of the caller. Omitting it is a 400: "orderId and type are
        // required" -- which is the one place IBKR reports an alert failure through the status code.
        var details = await _apiClient.SendAsync<AlertDetails>(
            IbkrRequest.Get(path).WithQuery("type", "Q"),
            cancellationToken).ConfigureAwait(false);

        // An unknown identifier comes back as 200 carrying an error, so the transport reports
        // success and the deserialized alert is empty apart from this field. Left unchecked, a
        // caller would read an alert with no name, no conditions and no account as though it were
        // real.
        return details.Error is null
            ? details
            : throw new IbkrApiException($"GET {path} did not return alert {alertId}: {details.Error}")
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Method = HttpMethod.Get.Method,
                Path = path,
                ResponseBody = details.Error,
            };
    }

    /// <inheritdoc />
    public Task<AlertDetails> GetMobileTradingAssistantAsync(
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AlertDetails>(
            IbkrRequest.Get("/v1/api/iserver/account/mta"),
            cancellationToken);

    /// <inheritdoc />
    public Task<AlertActionResult> SetActiveAsync(
        AccountId accountId,
        OrderId alertId,
        bool active,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AlertActionResult>(
            IbkrRequest
                .Post($"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}/alert/activate")
                .WithJsonBody(new ActivateAlertRequest(alertId.Value, active ? 1 : 0)),
            cancellationToken);

    /// <inheritdoc />
    public Task<AlertActionResult> DeleteAsync(
        AccountId accountId,
        OrderId alertId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AlertActionResult>(
            IbkrRequest.Delete(
                $"/v1/api/iserver/account/{IbkrRequest.PathSegment(accountId.Value)}" +
                $"/alert/{IbkrRequest.PathSegment(alertId.Value)}"),
            cancellationToken);

    // alertActive is documented as an enum over 1 and 0 rather than as a boolean, so it is sent as
    // the number IBKR asks for instead of as true/false.
    private sealed record ActivateAlertRequest(
        [property: JsonPropertyName("alertId")] long AlertId,
        [property: JsonPropertyName("alertActive")] int AlertActive);
}
