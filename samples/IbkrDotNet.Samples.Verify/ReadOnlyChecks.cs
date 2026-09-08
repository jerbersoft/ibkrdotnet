using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Time;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// Every endpoint that leaves the account as it found it.
/// </summary>
/// <remarks>
/// Read-only here means no order is created, modified or cancelled. A market data subscription and a
/// position-cache invalidation are both included: they touch gateway state rather than the account,
/// and the sweep cleans up after itself.
/// </remarks>
internal static class ReadOnlyChecks
{
    public static async Task<Context> RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        CancellationToken cancellationToken)
    {
        probe.Group("Session");
        await probe.RunAsync("POST /iserver/auth/status", async () =>
        {
            var status = await ibkr.Session.GetStatusAsync(cancellationToken);
            return $"connected={status.Connected} established={status.Established} competing={status.Competing}";
        });

        await probe.RunAsync("POST /tickle", async () =>
            $"ssoExpires={(await ibkr.Sessions.TickleAsync(cancellationToken)).SsoExpires}");

        await probe.RunAsync("GET  /sso/validate", async () =>
        {
            var validation = await ibkr.Sessions.ValidateAsync(cancellationToken);
            return $"result={validation.Result} expires {validation.Expires}";
        });

        probe.Skip("POST /logout", "would end the session the rest of the sweep depends on");

        probe.Group("Accounts");
        await probe.RunAsync("GET  /iserver/accounts", async () =>
            $"{(await ibkr.Accounts.GetTradableAccountsAsync(cancellationToken)).Accounts.Count} tradable");

        await probe.RunAsync("GET  /iserver/account/{a}/summary", async () =>
            $"netLiquidation={(await ibkr.Accounts.GetSummaryAsync(account, cancellationToken)).NetLiquidationValue}");

        await probe.RunAsync("GET  .../summary/balances", async () =>
            $"{(await ibkr.Accounts.GetBalanceSummaryAsync(account, cancellationToken)).Segments.Count} segment(s)");

        await probe.RunAsync("GET  .../summary/margins", async () =>
            $"{(await ibkr.Accounts.GetMarginSummaryAsync(account, cancellationToken)).Segments.Count} segment(s)");

        // Keyed by currency rather than by segment, unlike the three summaries around it.
        await probe.RunAsync("GET  .../summary/market_value", async () =>
            $"{(await ibkr.Accounts.GetMarketValueSummaryAsync(account, cancellationToken)).Segments.Count} currency(ies)");

        await probe.RunAsync("GET  .../summary/available_funds", async () =>
            $"{(await ibkr.Accounts.GetAvailableFundsSummaryAsync(account, cancellationToken)).Segments.Count} segment(s)");

        await probe.RunAsync("GET  /iserver/account/pnl/partitioned", async () =>
            $"{(await ibkr.Accounts.GetProfitAndLossAsync(cancellationToken)).Partitions.Count} partition(s)");

        await probe.RunAsync("GET  /acesws/{a}/signatures-and-owners", async () =>
            $"{(await ibkr.Accounts.GetOwnersAsync(account, cancellationToken)).Users.Count} user(s)");

        probe.Skip("GET  /iserver/account/search/{p}", "requires a Financial Advisor account structure");
        probe.Skip("POST /iserver/account", "switches the account later requests target");
        probe.Skip("POST /iserver/dynaccount", "requires a dynamic account structure");

        probe.Group("Portfolio");
        var accounts = await ibkr.Portfolio.GetAccountsAsync(cancellationToken);
        await probe.RunAsync("GET  /portfolio/accounts", () =>
            Task.FromResult($"{accounts.Count} account(s)"));

        await probe.RunAsync("GET  /portfolio/subaccounts", async () =>
            $"{(await ibkr.Portfolio.GetSubaccountsAsync(cancellationToken)).Count} subaccount(s)");

        await probe.RunAsync("GET  /portfolio/subaccounts2", async () =>
        {
            var page = await ibkr.Portfolio.GetSubaccountsPageAsync(0, cancellationToken);
            return $"{page.Subaccounts.Count} on page, total={page.Metadata?.Total}";
        });

        await probe.RunAsync("GET  /portfolio/{a}/meta", async () =>
            $"currency={(await ibkr.Portfolio.GetAccountMetadataAsync(account, cancellationToken)).Currency}");

        await probe.RunAsync("GET  /portfolio/{a}/summary", async () =>
            $"{(await ibkr.Portfolio.GetSummaryAsync(account, cancellationToken)).Count} field(s)");

        await probe.RunAsync("GET  /portfolio/{a}/ledger", async () =>
        {
            var ledger = await ibkr.Portfolio.GetLedgerAsync(account, cancellationToken);
            var currency = ledger.TryGetValue("BASE", out var entry) ? $" base as of {entry.RetrievedAt}" : string.Empty;
            return $"{ledger.Count} currency(ies){currency}";
        });

        await probe.RunAsync("GET  /portfolio/{a}/allocation", async () =>
        {
            var allocation = await ibkr.Portfolio.GetAllocationAsync(account, cancellationToken);
            return $"{allocation.AssetClass?.LongPositions.Count ?? 0} long asset class(es)";
        });

        var positions = Array.Empty<Trading.Models.Portfolio.Position>();
        await probe.RunAsync("GET  /portfolio/{a}/positions/0", async () =>
        {
            positions = [.. await ibkr.Portfolio.GetPositionsAsync(account, 0, cancellationToken)];
            return $"{positions.Length} position(s)";
        });

        await probe.RunAsync("GET  /portfolio/{a}/combo/positions", async () =>
        {
            // IBKR answers the first call with 500 {"error":"Not ready"} while the position cache
            // warms, the same warm-up that makes /portfolio/accounts a prerequisite elsewhere.
            try
            {
                return $"{(await ibkr.Portfolio.GetComboPositionsAsync(account, cancellationToken)).Count} combo(s)";
            }
            catch (IbkrApiException ex) when (ex.ResponseBody?.Contains("Not ready", StringComparison.Ordinal) is true)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                var combos = await ibkr.Portfolio.GetComboPositionsAsync(account, cancellationToken);
                return $"{combos.Count} combo(s), after one retry past \"Not ready\"";
            }
        });

        await probe.RunAsync("GET  /portfolio2/{a}/positions", async () =>
            $"{(await ibkr.Portfolio.GetUncachedPositionsAsync(account, cancellationToken: cancellationToken)).Count} position(s)");

        probe.Group("Contracts");
        var conId = new ConId(0);
        string? optionMonth = null;

        await probe.RunAsync("GET  /iserver/secdef/search", async () =>
        {
            var results = await ibkr.Contracts.SearchAsync("AAPL", cancellationToken: cancellationToken);
            if (results.Count == 0)
            {
                throw new InvalidOperationException("No result for AAPL; the rest of this group has nothing to ask about.");
            }

            conId = results[0].ConId;
            optionMonth = results[0].Sections
                .FirstOrDefault(s => s.SecurityType == "OPT")?.Months?.Split(';').FirstOrDefault();

            return $"{results.Count} hit(s), AAPL is {conId}";
        });

        // Both position endpoints keyed by instrument, asked about a contract that is certainly real
        // whether or not the account holds it.
        await probe.RunAsync("GET  /portfolio/{a}/position/{c}", async () =>
            $"{(await ibkr.Portfolio.GetPositionAsync(account, conId, cancellationToken)).Count} position(s)");

        await probe.RunAsync("GET  /portfolio/positions/{c}", async () =>
            $"{(await ibkr.Portfolio.GetPositionsByInstrumentAsync(conId, cancellationToken)).Count} account(s)");

        await probe.RunAsync("GET  /iserver/contract/{c}/info", async () =>
            $"{(await ibkr.Contracts.GetInfoAsync(conId, cancellationToken)).Symbol}");

        await probe.RunAsync("GET  /iserver/contract/{c}/info-and-rules", async () =>
            $"{(await ibkr.Contracts.GetInfoAndRulesAsync(conId, cancellationToken)).Symbol}");

        await probe.RunAsync("POST /iserver/contract/rules", async () =>
        {
            var rules = await ibkr.Contracts.GetRulesAsync(conId, cancellationToken: cancellationToken);
            return $"{rules.OrderTypes.Count} order type(s)";
        });

        await probe.RunAsync("GET  /iserver/contract/{c}/algos", async () =>
            $"{(await ibkr.Contracts.GetAlgorithmsAsync(conId, cancellationToken: cancellationToken)).Algorithms.Count} algo(s)");

        if (optionMonth is { Length: > 0 } month)
        {
            decimal? strike = null;
            await probe.RunAsync("GET  /iserver/secdef/strikes", async () =>
            {
                var strikes = await ibkr.Contracts.GetStrikesAsync(conId, "OPT", month, cancellationToken: cancellationToken);
                strike = strikes.Call.Count > 0 ? strikes.Call[strikes.Call.Count / 2] : null;
                return $"{strikes.Call.Count} call, {strikes.Put.Count} put ({month})";
            });

            if (strike is { } atStrike)
            {
                // Asked after the strikes, not before: for an option IBKR rejects the request
                // without a strike and a right, so there is nothing to ask until one is known.
                await probe.RunAsync("GET  /iserver/secdef/info", async () =>
                {
                    var attributes = await ibkr.Contracts.GetAttributesAsync(
                        conId,
                        "OPT",
                        month,
                        strike: atStrike.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        right: "C",
                        cancellationToken: cancellationToken);

                    return $"{attributes.Count} contract(s) at {atStrike} C";
                });
            }
            else
            {
                probe.Skip("GET  /iserver/secdef/info", "no strike came back to ask about");
            }
        }
        else
        {
            probe.Skip("GET  /iserver/secdef/strikes", "the search returned no option months to ask about");
            probe.Skip("GET  /iserver/secdef/info", "the search returned no option months to ask about");
        }

        probe.Skip("GET  /iserver/secdef/bond-filters", "needs an issuerId, which only a bond search produces");

        // The documented example shows displayRule as an array; a live gateway sends a bare object.
        await probe.RunAsync("GET  /trsrv/secdef", async () =>
            $"{(await ibkr.Contracts.GetDefinitionsAsync([conId], cancellationToken)).Definitions.Count} definition(s)");

        await probe.RunAsync("GET  /trsrv/stocks", async () =>
            $"{(await ibkr.Contracts.GetStocksAsync(["AAPL"], cancellationToken)).Count} symbol(s)");

        await probe.RunAsync("GET  /trsrv/futures", async () =>
            $"{(await ibkr.Contracts.GetFuturesAsync(["ES"], cancellationToken: cancellationToken)).Count} symbol(s)");

        await probe.RunAsync("GET  /trsrv/all-conids", async () =>
            $"{(await ibkr.Contracts.GetListingsByExchangeAsync("NASDAQ", "STK", cancellationToken)).Count} listing(s)");

        await probe.RunAsync("GET  /trsrv/secdef/schedule", async () =>
        {
            var schedules = await ibkr.Contracts.GetTradingScheduleAsync(
                "STK", "AAPL", "NASDAQ", cancellationToken: cancellationToken);
            return $"{schedules.Count} schedule(s), zone={(schedules.Count > 0 ? schedules[0].TimeZone?.Id : null)}";
        });

        await probe.RunAsync("GET  /iserver/currency/pairs", async () =>
            $"{(await ibkr.Contracts.GetCurrencyPairsAsync("USD", cancellationToken)).Count} group(s)");

        await probe.RunAsync("GET  /iserver/exchangerate", async () =>
            $"EUR/USD = {(await ibkr.Contracts.GetExchangeRateAsync("EUR", "USD", cancellationToken)).Rate}");

        probe.Group("Market data");
        decimal? reference = null;

        await probe.RunAsync("GET  /iserver/marketdata/snapshot", async () =>
        {
            // The first request is a pre-flight that starts IBKR streaming and returns nothing. The
            // quote is read from the stream on the second.
            await ibkr.MarketData.GetSnapshotAsync([conId], MarketDataField.TopOfBook, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var snapshot = await ibkr.MarketData.GetSnapshotAsync([conId], MarketDataField.TopOfBook, cancellationToken);

            reference = snapshot[0].LastPrice ?? snapshot[0].BidPrice;
            return $"last={snapshot[0].LastPrice} bid={snapshot[0].BidPrice} ask={snapshot[0].AskPrice} " +
                   $"({snapshot[0].MarketDataAvailability})";
        });

        await probe.RunAsync("GET  /iserver/marketdata/history", async () =>
        {
            var history = await ibkr.MarketData.GetHistoryAsync(
                conId, HistoryPeriod.OneWeek, BarSize.OneDay, cancellationToken: cancellationToken);
            return $"{history.Bars.Count} bar(s), last close {(history.Bars.Count > 0 ? history.Bars[^1].Close : null)}";
        });

        await probe.RunAsync("POST /iserver/marketdata/unsubscribe", async () =>
            $"success={(await ibkr.MarketData.UnsubscribeAsync(conId, cancellationToken)).Success}");

        await probe.RunAsync("GET  /iserver/marketdata/unsubscribeall", async () =>
            $"unsubscribed={(await ibkr.MarketData.UnsubscribeAllAsync(cancellationToken)).Unsubscribed}");

        probe.Group("Orders (read-only)");
        await probe.RunAsync("GET  /iserver/account/orders", async () =>
        {
            // The endpoint lists orders filled or cancelled during this session as well as working
            // ones, so a bare count reads as far more open interest than the account actually has.
            var orders = (await ibkr.Orders.GetOpenOrdersAsync(cancellationToken: cancellationToken)).Orders;
            var working = orders.Count(o =>
                o.Status is not ("Cancelled" or "Filled" or "Inactive"));

            return $"{working} working, {orders.Count} this session";
        });

        await probe.RunAsync("GET  /iserver/account/trades", async () =>
            $"{(await ibkr.Orders.GetTradesAsync(cancellationToken: cancellationToken)).Count} trade(s)");

        // A preview asks IBKR what the order would do. Nothing is submitted, but it carries a JSON
        // body, so it is also the cheapest check that request bodies still reach IBKR at all.
        await probe.RunAsync("POST /iserver/account/{a}/orders/whatif", async () =>
        {
            var preview = await ibkr.Orders.PreviewAsync(
                account,
                [
                    new OrderTicket
                    {
                        ConId = conId.Value,
                        OrderType = "MKT",
                        Side = OrderSide.Buy,
                        TimeInForce = TimeInForce.Day,
                        Quantity = 1,
                    },
                ],
                cancellationToken);

            return $"cost={preview.Amount?.Amount} commission={preview.Amount?.Commission}";
        });

        probe.Skip("POST /iserver/notification", "needs a server prompt that is only raised mid-order");

        // Invalidates the gateway's position cache, not anything on the account.
        await probe.RunAsync("POST /portfolio/{a}/positions/invalidate", async () =>
            $"{(await ibkr.Portfolio.InvalidatePositionCacheAsync(account, cancellationToken)).Message}");

        return new Context(conId, reference);
    }

    /// <summary>What the read-only sweep learned that the order checks need.</summary>
    internal sealed record Context(ConId ConId, decimal? ReferencePrice);
}
