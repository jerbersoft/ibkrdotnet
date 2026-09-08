using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Time;
using NodaTime;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IPortfolioAnalystClient" />
public sealed class PortfolioAnalystClient(IIbkrApiClient apiClient) : IPortfolioAnalystClient
{
    /// <summary>IBKR's own documented default, sent for the caller when they name no currency.</summary>
    private const string DefaultCurrency = "USD";

    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<AccountPerformance> GetPerformanceAsync(
        IEnumerable<AccountId> accountIds,
        PerformancePeriod period,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<AccountPerformance>(
            IbkrRequest.Post("/v1/api/pa/performance")
                .WithJsonBody(new PerformanceRequest(RequireAccounts(accountIds), period.ToWireValue())),
            cancellationToken);

    /// <inheritdoc />
    public Task<PerformanceAllPeriods> GetAllPeriodsPerformanceAsync(
        IEnumerable<AccountId> accountIds,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<PerformanceAllPeriods>(
            IbkrRequest.Post("/v1/api/pa/allperiods")
                .WithJsonBody(new AllPeriodsRequest(RequireAccounts(accountIds))),
            cancellationToken);

    /// <inheritdoc />
    public Task<TransactionHistory> GetTransactionsAsync(
        IEnumerable<AccountId> accountIds,
        IEnumerable<ConId> conIds,
        string? currency = null,
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conIds);
        var contracts = conIds.Select(conId => conId.Value).ToArray();
        if (contracts.Length == 0)
        {
            throw new ArgumentException(
                "At least one contract identifier is required.",
                nameof(conIds));
        }

        return _apiClient.SendAsync<TransactionHistory>(
            IbkrRequest.Post("/v1/api/pa/transactions")
                .WithJsonBody(new TransactionsRequest(
                    RequireAccounts(accountIds),
                    contracts,
                    // Always sent, despite IBKR documenting it as optional with a default of USD.
                    // It is not optional: omitting it is
                    // "Bad Request: acctIds, currency and conids are required", which costs the
                    // caller the endpoint's fifteen-minute window to discover. IBKR's own
                    // documented default is filled in instead.
                    currency ?? DefaultCurrency,
                    days)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PortfolioAllocation> GetAllocationAsync(
        IEnumerable<AccountId> accountIds,
        PortfolioAllocationType type,
        string? currency = null,
        LocalDate? asOfDate = null,
        string? model = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<PortfolioAllocation>(
            IbkrRequest.Post("/v1/api/pa/allocation")
                .WithJsonBody(new AllocationRequest(
                    RequireAccounts(accountIds),
                    type.ToWireValue(),
                    currency,
                    asOfDate is { } day ? IbkrTimePatterns.Date.Format(day) : null,
                    model)),
            cancellationToken);

    // IBKR's two descriptions of these endpoints disagree about whether acctIds is required -- the
    // reference calls it optional and shows an empty request body, the endpoint guide calls it
    // required -- and the cost of finding out the hard way is a fifteen-minute wait before the next
    // attempt. The endpoint guide is right, at least on /pa/transactions, which answers a request
    // without it "Bad Request: acctIds, currency and conids are required". The stricter reading is
    // enforced on all four, because a caller always knows which account they mean and nobody is
    // served by spending a window to discover that IBKR wanted it.
    private static string[] RequireAccounts(IEnumerable<AccountId> accountIds)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        var accounts = accountIds.Select(accountId => accountId.Value).ToArray();
        return accounts.Length > 0
            ? accounts
            : throw new ArgumentException(
                "At least one account identifier is required.",
                nameof(accountIds));
    }

    private sealed record PerformanceRequest(
        [property: JsonPropertyName("acctIds")] IReadOnlyList<string> AccountIds,
        [property: JsonPropertyName("period")] string Period);

    private sealed record AllPeriodsRequest(
        [property: JsonPropertyName("acctIds")] IReadOnlyList<string> AccountIds);

    private sealed record TransactionsRequest(
        [property: JsonPropertyName("acctIds")] IReadOnlyList<string> AccountIds,
        [property: JsonPropertyName("conids")] IReadOnlyList<long> ConIds,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("days")] int? Days);

    private sealed record AllocationRequest(
        [property: JsonPropertyName("acctIds")] IReadOnlyList<string> AccountIds,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("model")] string? Model);
}
