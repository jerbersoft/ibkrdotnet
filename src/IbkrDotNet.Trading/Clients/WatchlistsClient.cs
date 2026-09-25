using System.Globalization;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Watchlists;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IWatchlistsClient" />
public sealed class WatchlistsClient(IIbkrApiClient apiClient) : IWatchlistsClient
{
    /// <summary>The only value IBKR accepts for the <c>SC</c> filter.</summary>
    private const string UserWatchlistFilter = "USER_WATCHLIST";

    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public async Task<IReadOnlyList<WatchlistSummary>> GetAllAsync(
        bool userCreatedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SendAsync<WatchlistsResponse>(
            IbkrRequest.Get("/v1/api/iserver/watchlists")
                .WithQuery("SC", userCreatedOnly ? UserWatchlistFilter : null),
            cancellationToken).ConfigureAwait(false);

        return response.Data?.UserLists ?? [];
    }

    /// <inheritdoc />
    public Task<Watchlist> GetAsync(string watchlistId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchlistId);
        return _apiClient.SendAsync<Watchlist>(
            IbkrRequest.Get("/v1/api/iserver/watchlist").WithQuery("id", watchlistId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Watchlist> CreateAsync(
        string watchlistId,
        string name,
        IEnumerable<ConId> conIds,
        CancellationToken cancellationToken = default)
    {
        ValidateId(watchlistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(conIds);

        var rows = conIds
            .Select(conId => new CreateWatchlistRow(conId.Value.ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        var created = await _apiClient.SendAsync<CreatedWatchlistResponse>(
            IbkrRequest.Post("/v1/api/iserver/watchlist")
                .WithJsonBody(new CreateWatchlistRequest(watchlistId, name, rows)),
            cancellationToken).ConfigureAwait(false);

        return new Watchlist
        {
            Id = created.Id,
            Name = created.Name,
            Hash = created.Hash,
            IsReadOnly = created.IsReadOnly,
        };
    }

    /// <inheritdoc />
    public async Task<WatchlistDeletion> DeleteAsync(
        string watchlistId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchlistId);

        var response = await _apiClient.SendAsync<DeleteWatchlistResponse>(
            IbkrRequest.Delete("/v1/api/iserver/watchlist").WithQuery("id", watchlistId),
            cancellationToken).ConfigureAwait(false);

        return new WatchlistDeletion { DeletedId = response.Data?.Deleted };
    }

    // IBKR documents the identifier as digits only, yet gave some older lists negative identifiers
    // and takes writes under them, so a leading minus is allowed. Anything else is still refused
    // here rather than sent: IBKR does not document what it does with it, and failing on the call
    // is clearer than finding out.
    private static void ValidateId(string watchlistId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchlistId);
        var digits = watchlistId.AsSpan(watchlistId.StartsWith('-') ? 1 : 0);
        if (digits.IsEmpty || digits.ContainsAnyExceptInRange('0', '9'))
        {
            throw new ArgumentException(
                $"A watchlist identifier is a whole number, digits with an optional leading minus; '{watchlistId}' is not.",
                nameof(watchlistId));
        }
    }
}
