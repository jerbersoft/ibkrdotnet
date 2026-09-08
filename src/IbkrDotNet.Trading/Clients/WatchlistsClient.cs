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

    // Checked here rather than left to IBKR, which documents the constraint but not what it does
    // when the constraint is broken. Failing on the call is clearer than either outcome.
    private static void ValidateId(string watchlistId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchlistId);
        foreach (var character in watchlistId)
        {
            if (!char.IsAsciiDigit(character))
            {
                throw new ArgumentException(
                    $"IBKR requires a watchlist identifier of digits only; '{watchlistId}' is not.",
                    nameof(watchlistId));
            }
        }
    }
}
