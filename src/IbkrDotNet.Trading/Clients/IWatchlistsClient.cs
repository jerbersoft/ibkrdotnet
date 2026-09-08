using IbkrDotNet.Trading.Models.Watchlists;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The Watchlists endpoints, covering the instrument lists saved against the username.
/// </summary>
/// <remarks>
/// Watchlists belong to the username rather than to an account, and are the same lists Trader
/// Workstation and Client Portal show. Creating or deleting one through this client changes what the
/// user sees in those applications.
/// </remarks>
public interface IWatchlistsClient
{
    /// <summary>
    /// Lists the watchlists saved for the username.
    /// </summary>
    /// <param name="userCreatedOnly">
    /// Whether to exclude the watchlists IBKR creates and return only the user's own.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// The listing carries no instruments. Read one with
    /// <see cref="GetAsync(string, CancellationToken)"/> to see what it holds.
    /// </remarks>
    Task<IReadOnlyList<WatchlistSummary>> GetAllAsync(
        bool userCreatedOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a single watchlist and the instruments it holds.
    /// </summary>
    /// <param name="watchlistId">The watchlist identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<Watchlist> GetAsync(string watchlistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a watchlist from a set of instruments.
    /// </summary>
    /// <param name="watchlistId">
    /// The identifier to create the watchlist under. IBKR requires digits only, and requires it to
    /// be unique among the username's watchlists; it does not document what happens when it is not,
    /// so check <see cref="GetAllAsync(bool, CancellationToken)"/> first rather than risk displacing
    /// a list the user created elsewhere.
    /// </param>
    /// <param name="name">The display name, shown in Trader Workstation and Client Portal.</param>
    /// <param name="conIds">The instruments to put in the watchlist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="watchlistId"/> is empty or contains anything but digits, or
    /// <paramref name="name"/> is empty.
    /// </exception>
    /// <remarks>
    /// The returned watchlist holds no instruments whatever was submitted: IBKR documents the
    /// creation response's instrument array as always empty. Read the watchlist back with
    /// <see cref="GetAsync(string, CancellationToken)"/> to confirm its contents.
    /// </remarks>
    Task<Watchlist> CreateAsync(
        string watchlistId,
        string name,
        IEnumerable<ConId> conIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a watchlist from the username's settings.
    /// </summary>
    /// <param name="watchlistId">The watchlist identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchlistDeletion> DeleteAsync(
        string watchlistId,
        CancellationToken cancellationToken = default);
}
