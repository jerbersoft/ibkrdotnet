namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// Runs one check per endpoint and collects the outcomes into a report.
/// </summary>
/// <remarks>
/// A check passes if the call returns and deserializes. It is deliberately not an assertion about
/// the data: what comes back depends on the account, the venue and the hour, and a tool that failed
/// whenever a paper account held no positions would be ignored within a week. The bugs this catches
/// are transport and schema bugs, which surface as an exception either way.
/// </remarks>
internal sealed class Probe(string accountId, string maskedAccountId)
{
    private readonly List<Outcome> _outcomes = [];
    private string _group = string.Empty;

    public int Failures => _outcomes.Count(o => o.Status is Status.Failed);

    public void Group(string name)
    {
        _group = name;
        Console.WriteLine();
        Console.WriteLine(name);
    }

    /// <summary>Runs a check, recording whatever it returns as the endpoint's summary.</summary>
    public async Task RunAsync(string endpoint, Func<Task<string>> check)
    {
        try
        {
            var summary = await check();
            Record(new Outcome(_group, endpoint, Status.Passed, summary));
        }
        catch (SkipCheckException ex)
        {
            // A check that discovers mid-flight that it cannot run says so rather than failing.
            Record(new Outcome(_group, endpoint, Status.Skipped, ex.Message));
        }
        catch (Exception ex)
        {
            // Reported in full rather than truncated to a line. The `displayRule` schema mismatch
            // was legible only from the JSON path in the inner exception, which a short message cuts
            // off, and a report that hides the one useful detail is worse than no report.
            var detail = ex.InnerException is null
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{ex.GetType().Name}: {ex.Message}{System.Environment.NewLine}{ex.InnerException.Message}";

            Record(new Outcome(_group, endpoint, Status.Failed, detail));
        }
    }

    /// <summary>Records an endpoint that was exercised as part of another check.</summary>
    public void Pass(string endpoint, string summary) =>
        Record(new Outcome(_group, endpoint, Status.Passed, summary));

    /// <summary>Records an endpoint this run cannot reach, and why.</summary>
    public void Skip(string endpoint, string reason) =>
        Record(new Outcome(_group, endpoint, Status.Skipped, reason));

    /// <summary>Prints the tally. Returns true when nothing failed.</summary>
    public bool Report()
    {
        var passed = _outcomes.Count(o => o.Status is Status.Passed);
        var skipped = _outcomes.Count(o => o.Status is Status.Skipped);

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {Failures} failed, {skipped} skipped.");

        if (Failures > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed:");
            foreach (var outcome in _outcomes.Where(o => o.Status is Status.Failed))
            {
                Console.WriteLine($"  {outcome.Group} {outcome.Endpoint}");
                foreach (var line in outcome.Summary.Split(System.Environment.NewLine))
                {
                    Console.WriteLine($"      {line}");
                }
            }
        }

        return Failures == 0;
    }

    /// <summary>
    /// Removes the account identifier from anything on its way out.
    /// </summary>
    /// <remarks>
    /// Masking only the header would be theatre: IBKR puts the account in the request path, so it
    /// comes back inside every error message this tool exists to print.
    /// </remarks>
    private string Redact(string value) =>
        value.Replace(accountId, maskedAccountId, StringComparison.OrdinalIgnoreCase);

    private void Record(Outcome outcome)
    {
        outcome = outcome with { Summary = Redact(outcome.Summary) };
        _outcomes.Add(outcome);

        var label = outcome.Status switch
        {
            Status.Passed => "ok  ",
            Status.Failed => "FAIL",
            _ => "skip",
        };

        var summary = outcome.Summary.ReplaceLineEndings(" ");
        Console.WriteLine($"  {label}  {outcome.Endpoint,-44}  {Shorten(summary)}");
    }

    private static string Shorten(string value) =>
        value.Length <= 96 ? value : value[..96] + "...";

    private enum Status
    {
        Passed,
        Failed,
        Skipped,
    }

    private sealed record Outcome(string Group, string Endpoint, Status Status, string Summary);
}

/// <summary>
/// Thrown by a check that cannot run, to record a skip rather than a failure.
/// </summary>
/// <param name="reason">Why the check could not run.</param>
internal sealed class SkipCheckException(string reason) : Exception(reason);
