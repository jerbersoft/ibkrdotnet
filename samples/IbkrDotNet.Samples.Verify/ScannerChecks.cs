using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Scanner;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The market scanner endpoints.
/// </summary>
/// <remarks>
/// The scan is built from the parameters the first call returned rather than from hard-coded codes,
/// so a run also demonstrates that the reference payload can actually be used to construct a
/// request. Neither endpoint changes anything.
/// </remarks>
internal static class ScannerChecks
{
    /// <summary>The instrument type the sweep scans, when the gateway offers it.</summary>
    private const string PreferredInstrument = "STK";

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        CancellationToken cancellationToken)
    {
        probe.Group("Scanner");

        ScannerParameters? parameters = null;
        await probe.RunAsync("GET  /iserver/scanner/params", async () =>
        {
            try
            {
                parameters = await ibkr.Scanner.GetParametersAsync(cancellationToken);
            }
            catch (IbkrRateLimitExceededException ex)
            {
                // Not a failure. IBKR allows one call per fifteen minutes and the client refuses to
                // block that long, so a sweep run twice in a row legitimately cannot reach this.
                throw new SkipCheckException(
                    $"IBKR allows one call per 15 minutes, with {ex.RetryAfter} still to run");
            }

            return $"{parameters.ScanTypes.Count} scan type(s), {parameters.Instruments.Count} instrument(s), " +
                $"{parameters.Filters.Count} filter(s), {parameters.Locations.Count} location(s)";
        });

        if (parameters is null)
        {
            probe.Skip("POST /iserver/scanner/run", "no parameters, so no scan can be built");
            return;
        }

        if (Choose(parameters) is not { } choice)
        {
            probe.Skip("POST /iserver/scanner/run", "the parameters offer no scan this sweep can build");
            return;
        }

        var (instrument, scanType, location) = choice;

        await probe.RunAsync("POST /iserver/scanner/run", async () =>
        {
            var results = await ibkr.Scanner.RunAsync(
                instrument, scanType, location, cancellationToken: cancellationToken);

            // Whether the ranking value came back is worth reporting: IBKR documents it on every
            // row, and a live gateway has been seen to omit it from all of them, in and out of
            // regular trading hours.
            var ranked = results.Contracts.Count(c => c.ScanData is not null);
            var top = results.Contracts.Count > 0 ? results.Contracts[0].Symbol : "none";

            return $"{scanType} over {location}: {results.Contracts.Count} contract(s), top={top}, " +
                $"column={results.ScanDataColumnName}, {ranked} with a ranking value";
        });
    }

    /// <summary>
    /// Picks an instrument, a scan type valid for it and a location under it, from what the gateway
    /// actually offers.
    /// </summary>
    private static (string Instrument, string ScanType, string? Location)? Choose(ScannerParameters parameters)
    {
        var instrument =
            parameters.Instruments.FirstOrDefault(i => i.Type == PreferredInstrument)
            ?? parameters.Instruments.FirstOrDefault(i => i.Type is not null);

        if (instrument?.Type is not { } instrumentType)
        {
            return null;
        }

        var scanType = parameters.ScanTypes
            .FirstOrDefault(t => t.Code is not null && t.Instruments.Contains(instrumentType));

        if (scanType?.Code is not { } code)
        {
            return null;
        }

        // A location is optional, but scanning a whole instrument class without one is slower and
        // returns less interesting results, so take the first leaf under the matching root.
        var root = parameters.Locations.FirstOrDefault(l => l.Type == instrumentType);
        var location = root?.Locations.FirstOrDefault(l => l.Type is not null)?.Type ?? root?.Type;

        return (instrumentType, code, location);
    }
}
