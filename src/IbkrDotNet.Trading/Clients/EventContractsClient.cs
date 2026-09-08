using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.EventContracts;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IEventContractsClient" />
public sealed class EventContractsClient(IIbkrApiClient apiClient) : IEventContractsClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<EventContractCategoryTree> GetCategoryTreeAsync(
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<EventContractCategoryTree>(
            IbkrRequest.Get("/v1/api/forecast/category/tree"),
            cancellationToken);

    /// <inheritdoc />
    public Task<EventContractMarket> GetMarketAsync(
        ConId underlyingConId,
        string? exchange = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<EventContractMarket>(
            IbkrRequest.Get("/v1/api/forecast/contract/market")
                .WithQuery("underlyingConid", underlyingConId.Value)
                .WithQuery("exchange", exchange),
            cancellationToken);

    /// <inheritdoc />
    public Task<EventContractDetails> GetDetailsAsync(
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<EventContractDetails>(
            IbkrRequest.Get("/v1/api/forecast/contract/details")
                .WithQuery("conid", conId.Value),
            cancellationToken);

    /// <inheritdoc />
    public Task<EventContractRules> GetRulesAsync(
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<EventContractRules>(
            IbkrRequest.Get("/v1/api/forecast/contract/rules")
                .WithQuery("conid", conId.Value),
            cancellationToken);

    /// <inheritdoc />
    public Task<EventContractSchedule> GetScheduleAsync(
        ConId conId,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<EventContractSchedule>(
            IbkrRequest.Get("/v1/api/forecast/contract/schedules")
                .WithQuery("conid", conId.Value),
            cancellationToken);
}
