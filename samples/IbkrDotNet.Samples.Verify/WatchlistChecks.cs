using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The watchlist endpoints, including creation and deletion.
/// </summary>
/// <remarks>
/// This is a write path, but not one that can move money: a watchlist is a saved instrument list on
/// the username, visible in Trader Workstation and Client Portal. The sweep creates one under a
/// fixed identifier, reads it back and deletes it in a <c>finally</c>. The identifier is checked
/// against the existing lists first, so a watchlist the user created is never displaced, and the
/// name says where it came from in case a run is killed before it can clean up.
/// </remarks>
internal static class WatchlistChecks
{
    /// <summary>The identifier the sweep creates its own watchlist under.</summary>
    /// <remarks>
    /// Fixed rather than random: if a run is interrupted between creating and deleting, the leftover
    /// is easier to find and remove when it is always the same one.
    /// </remarks>
    private const string WatchlistId = "9000001";

    private const string WatchlistName = "ibkrdotnet verify";

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        ReadOnlyChecks.Context context,
        CancellationToken cancellationToken)
    {
        probe.Group("Watchlists");

        IReadOnlyList<Trading.Models.Watchlists.WatchlistSummary> existing = [];
        await probe.RunAsync("GET  /iserver/watchlists", async () =>
        {
            existing = await ibkr.Watchlists.GetAllAsync(cancellationToken: cancellationToken);
            var mine = await ibkr.Watchlists.GetAllAsync(userCreatedOnly: true, cancellationToken);
            return $"{existing.Count} total, {mine.Count} user-created";
        });

        if (existing.Any(w => w.Id == WatchlistId))
        {
            probe.Skip("POST /iserver/watchlist", $"watchlist {WatchlistId} already exists");
            probe.Skip("DELETE /iserver/watchlist", $"nothing created, so nothing to delete");
            await ReadFirstExistingAsync(ibkr, probe, existing, cancellationToken);
            return;
        }

        var created = false;
        try
        {
            await probe.RunAsync("POST /iserver/watchlist", async () =>
            {
                // IBKR returns an empty instrument array here whatever was submitted, so the read
                // below is what actually shows the conid landed.
                var watchlist = await ibkr.Watchlists.CreateAsync(
                    WatchlistId, WatchlistName, [context.ConId], cancellationToken);

                created = true;
                return $"id={watchlist.Id} readOnly={watchlist.IsReadOnly}";
            });

            if (!created)
            {
                probe.Skip("GET  /iserver/watchlist", "creation failed, so there is nothing to read");
                return;
            }

            await probe.RunAsync("GET  /iserver/watchlist", async () =>
            {
                var watchlist = await ibkr.Watchlists.GetAsync(WatchlistId, cancellationToken);
                var instruments = watchlist.Instruments;
                var tickers = string.Join(", ", instruments.Select(i => i.Ticker ?? i.ConId.ToString()));
                return $"\"{watchlist.Name}\" holds {instruments.Count}: {tickers}";
            });
        }
        finally
        {
            if (created)
            {
                await probe.RunAsync("DELETE /iserver/watchlist", async () =>
                {
                    var deletion = await ibkr.Watchlists.DeleteAsync(WatchlistId, cancellationToken);

                    // Asked rather than assumed: the point of the check is that nothing is left over.
                    var remaining = await ibkr.Watchlists.GetAllAsync(cancellationToken: cancellationToken);
                    var stillThere = remaining.Any(w => w.Id == WatchlistId);
                    return $"deleted={deletion.DeletedId}; {(stillThere ? "STILL LISTED" : "gone from the listing")}";
                });
            }
        }
    }

    /// <summary>
    /// Reads whichever watchlist the username already has, so the read endpoint is still covered on
    /// a run that could not create its own.
    /// </summary>
    private static async Task ReadFirstExistingAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        IReadOnlyList<Trading.Models.Watchlists.WatchlistSummary> existing,
        CancellationToken cancellationToken)
    {
        if (existing.FirstOrDefault(w => w.Id is not null) is not { Id: { } id })
        {
            probe.Skip("GET  /iserver/watchlist", "the username has no watchlist to read");
            return;
        }

        await probe.RunAsync("GET  /iserver/watchlist", async () =>
            $"{(await ibkr.Watchlists.GetAsync(id, cancellationToken)).Instruments.Count} instrument(s)");
    }
}
