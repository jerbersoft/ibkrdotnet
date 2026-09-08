using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Models.EventContracts;
using IbkrDotNet.Trading.Primitives;
using NodaTime;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The event contract endpoints: five reads, all of them run.
/// </summary>
/// <remarks>
/// <para>
/// The whole group is discovery, so the sweep walks it as a chain rather than calling each endpoint
/// with a hard-coded identifier: the tree yields a market conid, the market yields a contract conid,
/// and that conid drives the remaining three. A run therefore proves the endpoints agree about which
/// identifier is which, which is the one thing that is easy to get wrong here -- a market carries
/// both a <c>conid</c> and a <c>product_conid</c>, and only the first is accepted by the market
/// endpoint.
/// </para>
/// <para>
/// Nothing here writes, and none of it needs a position or an order. The only way the group can
/// legitimately come up empty is if the account cannot see ForecastEx products at all, which is
/// reported as a skip rather than a failure.
/// </para>
/// </remarks>
internal static class EventContractChecks
{
    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        CancellationToken cancellationToken)
    {
        probe.Group("Event contracts");

        EventContractCategoryTree? tree = null;
        await probe.RunAsync("GET  /forecast/category/tree", async () =>
        {
            tree = await ibkr.EventContracts.GetCategoryTreeAsync(cancellationToken);

            var markets = tree.Categories.Values.Sum(c => c.Markets.Count);
            var roots = tree.Categories.Values.Count(c => c.ParentId is null);
            if (tree.Categories.Count == 0)
            {
                throw new SkipCheckException("the account sees no event contract categories");
            }

            var restricted = tree.Categories.Values
                .SelectMany(c => c.Markets)
                .Count(m => m.IsRestricted is true);
            Console.WriteLine($"      {roots} root(s), deepest chain {Depth(tree)} level(s)");
            if (restricted > 0)
            {
                Console.WriteLine($"      {restricted} market(s) restricted for this account");
            }

            return $"{tree.Categories.Count} categor(ies), {markets} market(s)";
        });

        if (Pick(tree) is not { } picked)
        {
            const string Reason = "the tree carries no market this account can look up";
            probe.Skip("GET  /forecast/contract/market", Reason);
            probe.Skip("GET  /forecast/contract/details", Reason);
            probe.Skip("GET  /forecast/contract/rules", Reason);
            probe.Skip("GET  /forecast/contract/schedules", Reason);
            return;
        }

        EventContractMarket? market = null;
        await probe.RunAsync("GET  /forecast/contract/market", async () =>
        {
            market = await ibkr.EventContracts.GetMarketAsync(
                picked.ConId!.Value, cancellationToken: cancellationToken);

            var sides = market.Contracts.Select(c => c.Side).Distinct().Count();
            Console.WriteLine($"      {picked.Name}  conid {picked.ConId!.Value}");

            return $"{market.Contracts.Count} contract(s), {sides} side(s), payout {market.Payout}";
        });

        if (market?.Contracts.FirstOrDefault(c => c.ConId is not null) is not { ConId: { } conId } leg)
        {
            const string Reason = "the market listed no contract to look up";
            probe.Skip("GET  /forecast/contract/details", Reason);
            probe.Skip("GET  /forecast/contract/rules", Reason);
            probe.Skip("GET  /forecast/contract/schedules", Reason);
            return;
        }

        await probe.RunAsync("GET  /forecast/contract/details", async () =>
        {
            var details = await ibkr.EventContracts.GetDetailsAsync(conId, cancellationToken);

            // The point of asking with one side's conid: the response should name both, and should
            // agree with the market listing about which side was requested.
            if (details.YesConId is null || details.NoConId is null)
            {
                throw new InvalidOperationException(
                    "the contract detail named only one side of the pair");
            }

            var echoed = details.Side == leg.Side ? "side agrees" : $"side differs ({details.Side})";
            Console.WriteLine($"      {Shorten(details.Question)}");

            return $"yes {details.YesConId}, no {details.NoConId}, {echoed}";
        });

        await probe.RunAsync("GET  /forecast/contract/rules", async () =>
        {
            var rules = await ibkr.EventContracts.GetRulesAsync(conId, cancellationToken);

            var settles = rules.SourceAgency is { Length: > 0 } agency ? agency : "no source agency";
            var when = rules.LastTradeTime is { } last
                ? last.InUtc().Date.ToString("uuuu-MM-dd", null)
                : "no last trade time";

            return $"{settles}, last trade {when}, threshold {Shorten(rules.Threshold)}";
        });

        await probe.RunAsync("GET  /forecast/contract/schedules", async () =>
        {
            var schedule = await ibkr.EventContracts.GetScheduleAsync(conId, cancellationToken);

            if (schedule.TimeZone is null)
            {
                throw new InvalidOperationException("the schedule named no time zone");
            }

            // Worth reporting: IBKR sends a TZDB link rather than a canonical zone id, and the whole
            // schedule is meaningless if it does not resolve.
            var blocks = schedule.TradingDays.Sum(d => d.TradingTimes.Count);
            var split = schedule.TradingDays.Count(d => d.TradingTimes.Count > 1);
            Console.WriteLine(
                $"      zone {schedule.TimeZone.Id}, {split} day(s) with an intraday closure");

            return $"{schedule.TradingDays.Count} day(s), {blocks} block(s)";
        });
    }

    /// <summary>
    /// Picks a market to walk down from, preferring one the account is not restricted from.
    /// </summary>
    private static EventContractMarketSummary? Pick(EventContractCategoryTree? tree) =>
        tree?.Categories.Values
            .SelectMany(category => category.Markets)
            .Where(market => market.ConId is not null)
            .OrderBy(market => market.IsRestricted is true)
            .ThenBy(market => market.ConId!.Value.Value)
            .FirstOrDefault();

    /// <summary>The deepest parent chain in the tree, as a sanity check that it really is a tree.</summary>
    private static int Depth(EventContractCategoryTree tree)
    {
        var deepest = 0;
        foreach (var id in tree.Categories.Keys)
        {
            var depth = 0;
            var current = id;

            // Bounded by the category count so a cycle in IBKR's data cannot hang the sweep.
            while (tree.Categories.TryGetValue(current, out var category)
                && category.ParentId is { } parent
                && depth < tree.Categories.Count)
            {
                depth++;
                current = parent;
            }

            deepest = Math.Max(deepest, depth);
        }

        return deepest + 1;
    }

    private static string Shorten(string? text) =>
        text is null ? "(none)"
        : text.Length <= 72 ? text
        : text[..69] + "...";
}
