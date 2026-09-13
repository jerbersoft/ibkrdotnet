// Sweeps every endpoint this library implements against a running Client Portal Gateway and reports
// one line per endpoint. It exists because the two bugs found in the first live run were both
// invisible to the unit tests: one lived in an HTTP intermediary that no stub reproduces, and the
// other in a documented example that the live API contradicts, so the fixture was wrong in exactly
// the same way the model was.
//
//   dotnet run --project samples/IbkrDotNet.Samples.Verify -- \
//       --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
//
// By default no order is submitted. Add --Orders=true to also exercise the order write path with a
// resting limit order that cannot fill.
//
// --Fills=true goes further and submits a market order for one share, reads back the execution, and
// sells it again to flatten. It is the only way to reach the four execution-time encodings, whose
// fixtures were transcribed from IBKR's documentation and had never been checked against the API.
//
// Both flags require a paper account and are refused on any other.
//
// The watchlist checks do write, because a watchlist cannot move money: one is created under a fixed
// identifier, read back and deleted again, and the identifier is checked against the existing lists
// first so nothing the user made is displaced.
//
// The streaming checks open the WebSocket, read what IBKR sends on connect, ping it, wait for a
// heartbeat and close it. No topic is subscribed; the transport is what is being verified.
//
// No credential is read, stored or printed here. The gateway holds the login and this talks to it
// over loopback, so there is nothing to configure and nothing to leak. The account identifier is
// discovered at runtime and masked on the way out, because this output is meant to be pasted into a
// bug report.

using System.Net.Security;
using IbkrDotNet.Extensions.DependencyInjection;
using IbkrDotNet.Samples.Verify;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Authentication;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Session;
using IbkrDotNet.Trading.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services
    .AddIbkrTrading(builder.Configuration.GetSection(IbkrTradingOptions.SectionName))
    .UseClientPortalGateway();

builder.Services.Configure<IbkrTradingOptions>(o => o.UserAgent = "ibkrdotnet-verify/0.1");

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

    // The WebSocket upgrade does not go through that handler, so the same loopback-only relaxation
    // is applied to the socket separately.
    builder.Services.Configure<IbkrTradingOptions>(o =>
    {
        var loopback = o.ResolveBaseAddress().IsLoopback;
        o.Streaming.ConfigureClientWebSocket = ws =>
            ws.RemoteCertificateValidationCallback = (_, _, _, errors) =>
                errors == SslPolicyErrors.None || loopback;
    });
}

using var host = builder.Build();
var ibkr = host.Services.GetRequiredService<IIbkrTradingClient>();
var gateway = host.Services.GetRequiredService<IOptions<IbkrTradingOptions>>().Value.ResolveBaseAddress();
var writeOrders = builder.Configuration.GetValue("Orders", defaultValue: false);
var fillOrders = builder.Configuration.GetValue("Fills", defaultValue: false);
var cancellationToken = CancellationToken.None;

Console.WriteLine($"Gateway: {gateway}");

AccountId account;
try
{
    var status = await ibkr.Session.EnsureBrokerageSessionAsync(cancellationToken);
    if (!status.IsReadyToTrade)
    {
        Console.Error.WriteLine($"The brokerage session is not ready. Log in at {gateway} and try again.");
        return 1;
    }

    // Other /portfolio endpoints return empty or stale data until the accounts have been listed.
    var accounts = await ibkr.Portfolio.GetAccountsAsync(cancellationToken);
    if (accounts.Count == 0)
    {
        Console.Error.WriteLine("The session reports no accounts.");
        return 1;
    }

    account = accounts[0].AccountId;
}
catch (IbkrAuthenticationException ex)
{
    Console.Error.WriteLine($"Not authenticated: {ex.Message}");
    Console.Error.WriteLine($"Log in at {gateway} and try again.");
    return 1;
}

Console.WriteLine($"Account: {Mask(account)}");

var probe = new Probe(account.Value, Mask(account));
var context = await ReadOnlyChecks.RunAsync(ibkr, probe, account, cancellationToken);

// Built by hand until the dependency injection package registers it.
await using (var streaming = new IbkrStreamingTransport(
    host.Services.GetRequiredService<IIbkrSessionManager>(),
    host.Services.GetRequiredService<IbkrSessionState>(),
    host.Services.GetRequiredService<IIbkrAuthenticator>(),
    host.Services.GetRequiredService<IOptions<IbkrTradingOptions>>(),
    host.Services.GetRequiredService<IClock>(),
    host.Services.GetRequiredService<ILogger<IbkrStreamingTransport>>()))
{
    await StreamingChecks.RunAsync(streaming, probe, cancellationToken);
}

await WatchlistChecks.RunAsync(ibkr, probe, context, cancellationToken);
await ScannerChecks.RunAsync(ibkr, probe, cancellationToken);
await NotificationChecks.RunAsync(ibkr, probe, cancellationToken);
await EventContractChecks.RunAsync(ibkr, probe, cancellationToken);
await AlertChecks.RunAsync(ibkr, probe, account, cancellationToken);
await PortfolioAnalystChecks.RunAsync(ibkr, probe, account, context, cancellationToken);

// IBKR gives paper accounts a DU prefix. There is deliberately no flag to override the check: an
// order sweep against a funded account should take more than a command line to arrange.
var paper = IsPaperAccount(account);

if (!writeOrders || !paper)
{
    probe.Group("Orders (write path)");
    probe.Skip(
        "the order write path",
        writeOrders ? $"{Mask(account)} is not a paper account" : "not requested; pass --Orders=true to include it");
}
else
{
    await OrderChecks.RunAsync(ibkr, probe, account, context, cancellationToken);
}

if (!fillOrders || !paper)
{
    probe.Group("Executions (filling order)");
    probe.Skip(
        "the fill path",
        fillOrders ? $"{Mask(account)} is not a paper account" : "not requested; pass --Fills=true to include it");
}
else
{
    await ExecutionChecks.RunAsync(ibkr, probe, account, context, cancellationToken);
}

return probe.Report() ? 0 : 1;

static bool IsPaperAccount(AccountId account) =>
    account.Value.StartsWith("DU", StringComparison.OrdinalIgnoreCase);

// Enough to tell two accounts apart in a report, not enough to identify one.
static string Mask(AccountId account) =>
    account.Value.Length <= 4
        ? account.Value
        : $"{account.Value[..2]}{new string('*', account.Value.Length - 4)}{account.Value[^2..]}";
