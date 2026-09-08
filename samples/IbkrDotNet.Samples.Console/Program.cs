// A read-only tour of the Trading API against a locally running Client Portal Gateway.
//
//   1. Start the gateway and log in at https://localhost:5000
//   2. dotnet run --project samples/IbkrDotNet.Samples.Console
//
// If the gateway is on another port (macOS uses 5000 for the AirPlay receiver, so moving it is
// common), point the sample at it and trust its self-signed certificate:
//
//   dotnet run --project samples/IbkrDotNet.Samples.Console -- \
//       --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
//
// This sample never places a live order. The only order-related call it makes is a preview
// (/orders/whatif), which asks IBKR what an order would do without submitting it.

using System.Net.Security;
using IbkrDotNet.Extensions.DependencyInjection;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Models.Orders;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services
    .AddIbkrTrading(builder.Configuration.GetSection(IbkrTradingOptions.SectionName))
    .UseClientPortalGateway();

builder.Services.Configure<IbkrTradingOptions>(o => o.UserAgent = "ibkrdotnet-sample/0.1");

// The gateway serves a self-signed certificate, which HttpClient rejects. Opting in relaxes
// validation for loopback addresses only, so the flag cannot quietly disable it for a real host.
if (builder.Configuration.GetValue("TrustGatewayCertificate", defaultValue: false))
{
    builder.Services
        .AddHttpClient(IbkrApiClient.HttpClientName)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == SslPolicyErrors.None || request.RequestUri?.IsLoopback is true,
        });
}

using var host = builder.Build();
var ibkr = host.Services.GetRequiredService<IIbkrTradingClient>();
var gateway = host.Services.GetRequiredService<IOptions<IbkrTradingOptions>>().Value.ResolveBaseAddress();
var cancellationToken = CancellationToken.None;

Console.WriteLine($"Gateway: {gateway}");

try
{
    Console.WriteLine("Establishing the brokerage session...");
    var status = await ibkr.Session.EnsureBrokerageSessionAsync(cancellationToken);

    Console.WriteLine(
        $"  connected={status.Connected} authenticated={status.Authenticated} " +
        $"established={status.Established} competing={status.Competing}");

    if (!status.IsReadyToTrade)
    {
        Console.WriteLine($"  The session is not ready. Log in at {gateway} and try again.");
        return 1;
    }

    var tradable = await ibkr.Accounts.GetTradableAccountsAsync(cancellationToken);
    Console.WriteLine($"\nTradable accounts: {string.Join(", ", tradable.Accounts)}");

    // /portfolio/accounts must be called before any other /portfolio endpoint for those accounts.
    var viewable = await ibkr.Portfolio.GetAccountsAsync(cancellationToken);
    var account = viewable.Count > 0 ? viewable[0].AccountId : tradable.Accounts[0];
    Console.WriteLine($"Using account {account}");

    var summary = await ibkr.Accounts.GetSummaryAsync(account, cancellationToken);
    Console.WriteLine($"\nNet liquidation: {summary.NetLiquidationValue:N2}");
    Console.WriteLine($"Available funds: {summary.AvailableFunds:N2}");
    Console.WriteLine($"Buying power:    {summary.BuyingPower:N2}");

    var ledger = await ibkr.Portfolio.GetLedgerAsync(account, cancellationToken);
    if (ledger.TryGetValue("BASE", out var baseCurrency))
    {
        Console.WriteLine(
            $"Base cash:       {baseCurrency.CashBalance:N2} " +
            $"(as of {baseCurrency.RetrievedAt})");
    }

    var search = await ibkr.Contracts.SearchAsync("AAPL", cancellationToken: cancellationToken);
    if (search.Count == 0)
    {
        Console.WriteLine("\nNo instrument found for AAPL; stopping here.");
        return 0;
    }

    var conId = search[0].ConId;
    Console.WriteLine($"\nAAPL is conid {conId} ({search[0].CompanyName})");

    // The first snapshot request for an instrument is a pre-flight: it starts IBKR streaming and
    // returns no data. Ask again to actually read the quote.
    await ibkr.MarketData.GetSnapshotAsync([conId], MarketDataField.TopOfBook, cancellationToken);
    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    var snapshot = await ibkr.MarketData.GetSnapshotAsync([conId], MarketDataField.TopOfBook, cancellationToken);

    Console.WriteLine(
        $"  last={snapshot[0].LastPrice} bid={snapshot[0].BidPrice} ask={snapshot[0].AskPrice} " +
        $"({snapshot[0].MarketDataAvailability})");

    var history = await ibkr.MarketData.GetHistoryAsync(
        conId,
        HistoryPeriod.OneWeek,
        BarSize.OneDay,
        cancellationToken: cancellationToken);

    Console.WriteLine($"\nLast {history.Bars.Count} daily bars:");
    foreach (var bar in history.Bars)
    {
        Console.WriteLine($"  {bar.Start}  O {bar.Open}  H {bar.High}  L {bar.Low}  C {bar.Close}");
    }

    // A preview only. Nothing below submits an order.
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

    Console.WriteLine("\nPreview of buying 1 share at market (not submitted):");
    Console.WriteLine($"  cost:        {preview.Amount?.Amount}");
    Console.WriteLine($"  commission:  {preview.Amount?.Commission}");
    Console.WriteLine($"  equity:      {preview.Equity?.Current} -> {preview.Equity?.After}");
    Console.WriteLine($"  init margin: {preview.InitialMargin?.Current} -> {preview.InitialMargin?.After}");
    if (!string.IsNullOrWhiteSpace(preview.Warning))
    {
        Console.WriteLine($"  warning:     {preview.Warning}");
    }

    // Streams left open consume the account's market data lines.
    await ibkr.MarketData.UnsubscribeAllAsync(cancellationToken);
    return 0;
}
catch (IbkrAuthenticationException ex)
{
    Console.Error.WriteLine($"Not authenticated: {ex.Message}");
    Console.Error.WriteLine($"Log in at {gateway} and try again.");
    return 1;
}
catch (IbkrApiException ex)
{
    Console.Error.WriteLine($"IBKR returned an error: {ex.Message}");
    return 1;
}
