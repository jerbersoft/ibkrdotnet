using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.PortfolioAnalyst;
using IbkrDotNet.Trading.Primitives;
using NodaTime;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The PortfolioAnalyst endpoints.
/// </summary>
/// <remarks>
/// <para>
/// All four are reads, so the whole group is safe to run against any account. What makes it awkward
/// is the pacing: one request per fifteen minutes each. The client will not block for that long, so
/// a sweep run twice in a row reports these as skipped rather than failing, the same way
/// <see cref="ScannerChecks"/> does.
/// </para>
/// <para>
/// Two of the checks re-prove claims the library's own documentation makes, rather than trusting
/// them: that <c>lastSuccessfulUpdate</c> is UTC, and that <c>nd</c> counts calendar days rather
/// than the data points IBKR calls it. Both are stated in the models, and both would drift silently
/// if nothing looked at them again.
/// </para>
/// </remarks>
internal static class PortfolioAnalystChecks
{
    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        ReadOnlyChecks.Context context,
        CancellationToken cancellationToken)
    {
        probe.Group("PortfolioAnalyst");
        AccountId[] accounts = [account];

        await probe.RunAsync("POST /pa/allperiods", () => PacedAsync(async () =>
        {
            var performance = await ibkr.PortfolioAnalyst.GetAllPeriodsPerformanceAsync(
                accounts, cancellationToken);

            if (!performance.Accounts.TryGetValue(account, out var history))
            {
                // The figures arrive under a property named after the account. If that lookup ever
                // stops working, the response deserialized into an empty shell and nothing else
                // would have said so.
                throw new InvalidOperationException(
                    $"the response carried no property for the account; it had " +
                    $"{performance.Accounts.Count} account key(s)");
            }

            var year = history.GetPeriod(PerformancePeriod.OneYear);

            Console.WriteLine(
                $"      {history.BaseCurrency}, {history.Start} to {history.End}, " +
                $"periods {string.Join(", ", history.PeriodNames)}");
            Console.WriteLine($"      1Y: {Describe(year)}");
            Console.WriteLine($"      {DescribeUpdatedAt(history.LastSuccessfulUpdate)}");

            return $"{history.Periods.Count} period(s), measure {performance.PortfolioMeasure}, " +
                $"{performance.DayCount} day(s)";
        }));

        await probe.RunAsync("POST /pa/performance", () => PacedAsync(async () =>
        {
            var performance = await ibkr.PortfolioAnalyst.GetPerformanceAsync(
                accounts, PerformancePeriod.OneMonth, cancellationToken);

            var nav = performance.NetAssetValue;
            var intervals = performance.TimePeriodPerformance;

            Console.WriteLine(
                $"      nav {DescribeSeries(nav)}, " +
                $"cumulative {DescribeSeries(performance.CumulativePerformance)}, " +
                $"intervals {DescribeSeries(intervals)}");
            Console.WriteLine($"      {DescribeDayCount(performance.DayCount, nav)}");

            // The one thing about this endpoint a caller has to know: the interval series is not
            // labelled with dates when it is not sampled daily.
            var labels = intervals?.Dates is { Count: > 0 } dates ? dates[0] : "(none)";
            return $"freq {intervals?.Frequency ?? "?"}, first interval label \"{labels}\"";
        }));

        await probe.RunAsync("POST /pa/allocation", () => PacedAsync(async () =>
        {
            var allocation = await ibkr.PortfolioAnalyst.GetAllocationAsync(
                accounts, PortfolioAllocationType.All, cancellationToken: cancellationToken);

            foreach (var (category, breakdown) in allocation.Allocations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Console.WriteLine(
                    $"      {category}: long {DescribeSide(breakdown.LongPositions)}, " +
                    $"short {DescribeSide(breakdown.ShortPositions)}");
            }

            // Asking for ALL is one window instead of five, and IBKR never keys the response by it.
            var keyedByAll = allocation.GetBreakdown(PortfolioAllocationType.All) is not null;

            return $"{allocation.Allocations.Count} categor(ies) in {allocation.Currency}, " +
                $"as of {(allocation.Date is { } date ? date.ToString() : "now (no date sent)")}" +
                (keyedByAll ? ", keyed by ALL" : string.Empty);
        }));

        var conId = await ChooseContractAsync(ibkr, account, context, cancellationToken);

        await probe.RunAsync("POST /pa/transactions", () => PacedAsync(async () =>
        {
            var history = await ibkr.PortfolioAnalyst.GetTransactionsAsync(
                accounts, [conId], days: 365, cancellationToken: cancellationToken);

            var kinds = history.Transactions
                .Select(transaction => transaction.Type ?? "(none)")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);

            // IBKR documents the human-readable 'date' and not the machine-readable 'rawDate' the
            // model actually binds, so it is worth saying out loud that the undocumented one arrived.
            var dated = history.Transactions.Count(transaction => transaction.Date is not null);

            Console.WriteLine(
                $"      conid {conId}: {history.Transactions.Count} transaction(s), " +
                $"{dated} with a parseable date, kinds: {string.Join(", ", kinds)}");
            Console.WriteLine(
                $"      realized {history.RealizedProfitAndLoss?.Amount?.ToString() ?? "none"} " +
                $"over {history.RealizedProfitAndLoss?.Data.Count ?? 0} day(s)");

            return $"{history.Transactions.Count} transaction(s) over {history.DayCount} day(s) " +
                $"in {history.Currency}";
        }));
    }

    /// <summary>
    /// Prefers a contract the account actually holds, so the transaction history has something in
    /// it, and falls back to the contract the rest of the sweep is using.
    /// </summary>
    private static async Task<ConId> ChooseContractAsync(
        IIbkrTradingClient ibkr,
        AccountId account,
        ReadOnlyChecks.Context context,
        CancellationToken cancellationToken)
    {
        try
        {
            var positions = await ibkr.Portfolio.GetPositionsAsync(account, 0, cancellationToken);
            foreach (var position in positions)
            {
                if (position.ConId is { } held)
                {
                    return held;
                }
            }
        }
        catch (IbkrApiException)
        {
            // Reported by the portfolio checks already; this is only picking a contract.
        }

        return context.ConId;
    }

    private static async Task<string> PacedAsync(Func<Task<string>> check)
    {
        try
        {
            return await check();
        }
        catch (IbkrRateLimitExceededException ex)
        {
            // Two different things land here and they do not mean the same thing. The client
            // declining to block for the rest of a fifteen-minute window is expected on a second run
            // and is a skip. IBKR answering 429 means the window was actually spent by something
            // outside this client, which is worth saying plainly rather than filing under "skipped
            // as usual".
            var wait = ex.RetryAfter is { } retryAfter ? $", {retryAfter} still to run" : string.Empty;

            throw new SkipCheckException(ex.Limit is null
                ? $"IBKR answered 429: the window was spent outside this client{wait}"
                : $"IBKR allows one call per 15 minutes{wait}");
        }
    }

    /// <summary>
    /// Re-checks, on every run, the claim the library documents: that IBKR sends this field in UTC.
    /// </summary>
    private static string DescribeUpdatedAt(Instant? updatedAt)
    {
        if (updatedAt is not { } instant)
        {
            return "lastSuccessfulUpdate: absent";
        }

        var drift = SystemClock.Instance.GetCurrentInstant() - instant;
        var reading = drift < Duration.FromMinutes(5) && drift > Duration.FromMinutes(-5)
            ? "consistent with UTC"
            : $"{drift.TotalHours:0.0}h from now -- not UTC?";

        return $"lastSuccessfulUpdate {instant} ({reading})";
    }

    /// <summary>
    /// Reports what <c>nd</c> actually is beside the two things it might be. IBKR calls it the total
    /// data points; observed values match neither that nor the calendar width exactly, so this
    /// prints all three rather than asserting a rule that has already failed once.
    /// </summary>
    private static string DescribeDayCount(long? dayCount, PerformanceSeries? nav)
    {
        var points = nav?.Dates.Count ?? 0;
        var entry = nav?.Data.Count > 0 ? nav.Data[0] : null;
        if (dayCount is not { } days || entry?.Start is not { } start || entry.End is not { } end)
        {
            return $"nd {dayCount?.ToString() ?? "absent"} against {points} data point(s)";
        }

        var span = Period.Between(start, end, PeriodUnits.Days).Days + 1;
        var fromStartValue = entry.StartValue?.Date is { } opened
            ? Period.Between(opened, end, PeriodUnits.Days).Days + 1
            : span;

        return $"nd {days} against {points} data point(s), {span} calendar day(s) start..end, " +
            $"{fromStartValue} from the start value's date" +
            (days == points ? " -- data points after all?" : string.Empty);
    }

    private static string DescribeSeries(PerformanceSeries? series) =>
        series is null
            ? "absent"
            : $"{series.Dates.Count}x{series.Frequency ?? "?"}";

    private static string Describe(PerformancePeriodSeries? period) =>
        period is null
            ? "absent"
            : $"{period.Dates.Count} point(s) at {period.Frequency ?? "?"}, " +
                $"from {period.StartValue?.Value?.ToString() ?? "?"} on {period.StartValue?.Date?.ToString() ?? "?"}";

    private static string DescribeSide(AllocationSide? side) =>
        side is null
            ? "none"
            : $"{side.Total?.NetAssetValue?.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) ?? "?"} " +
                $"over {side.Items.Count} item(s)";
}
