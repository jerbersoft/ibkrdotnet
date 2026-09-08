using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Session;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="ISessionClient" />
public sealed class SessionClient(IIbkrApiClient apiClient) : ISessionClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<BrokerageSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<BrokerageSessionStatus>(
            IbkrRequest.Post("/v1/api/iserver/auth/status"),
            cancellationToken);

    /// <inheritdoc />
    public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<TickleResponse>(IbkrRequest.Post("/v1/api/tickle"), cancellationToken);

    /// <inheritdoc />
    public Task<BrokerageSessionStatus> InitializeAsync(
        bool compete = true,
        bool publish = true,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<BrokerageSessionStatus>(
            IbkrRequest.Post("/v1/api/iserver/auth/ssodh/init")
                .WithJsonBody(new InitializeBrokerageSessionRequest(compete, publish)),
            cancellationToken);

    /// <inheritdoc />
    public Task<SsoValidationResponse> ValidateAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<SsoValidationResponse>(
            IbkrRequest.Get("/v1/api/sso/validate"),
            cancellationToken);

    /// <inheritdoc />
    public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<LogoutResponse>(IbkrRequest.Post("/v1/api/logout"), cancellationToken);
}
