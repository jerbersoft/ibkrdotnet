using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The alert endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Only two of the five run unconditionally, because IBKR publishes no endpoint that creates an
/// alert: the sweep cannot make itself something to read, activate or delete the way
/// <see cref="WatchlistChecks"/> can. What it does instead is use whatever the account already has,
/// and say plainly what it skipped when the account has nothing.
/// </para>
/// <para>
/// The two writes are treated differently, because their consequences differ. Activation is
/// exercised by setting an existing alert to the state it is already in, which reaches the endpoint
/// and changes nothing. Deletion is not reversible through the API at all, so it runs only against
/// an alert this tool can prove was made for it -- one named <see cref="SweepAlertName"/> -- and is
/// skipped otherwise. An alert the user built in Trader Workstation is never touched.
/// </para>
/// </remarks>
internal static class AlertChecks
{
    /// <summary>The alert name that marks one as disposable.</summary>
    /// <remarks>
    /// Create an alert with this name in Trader Workstation to let a run exercise the delete
    /// endpoint. Nothing else about it matters; the sweep reads it, re-applies its current active
    /// state and then removes it.
    /// </remarks>
    private const string SweepAlertName = "ibkrdotnet verify";

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        AccountId account,
        CancellationToken cancellationToken)
    {
        probe.Group("Alerts");

        IReadOnlyList<Trading.Models.Alerts.AlertSummary> alerts = [];
        await probe.RunAsync("GET  /iserver/account/{id}/alerts", async () =>
        {
            alerts = await ibkr.Alerts.GetAllAsync(account, cancellationToken);
            if (alerts.Count == 0)
            {
                return "no alerts on the account";
            }

            var active = alerts.Count(a => a.IsActive is true);
            var triggered = alerts.Count(a => a.IsTriggered is true);
            return $"{alerts.Count} alert(s), {active} active, {triggered} triggered";
        });

        await probe.RunAsync("GET  /iserver/account/mta", async () =>
        {
            var mta = await ibkr.Alerts.GetMobileTradingAssistantAsync(cancellationToken);

            // Read a second time on purpose. IBKR documents the MTA alert's order id as changing
            // when the alert is modified; it in fact changes on every read, and a caller who keeps
            // it holds a number no endpoint will accept. Proving that here means the claim in the
            // library's own documentation is re-checked on every run rather than taken on trust.
            var again = await ibkr.Alerts.GetMobileTradingAssistantAsync(cancellationToken);
            var stable = mta.Id == again.Id;

            Console.WriteLine(
                $"      status {mta.OrderStatus ?? "(none)"}, {mta.Conditions.Count} condition(s), " +
                $"defaults {(mta.MtaDefaults is null ? "absent" : $"{mta.MtaDefaults.Split('|').Length} type(s)")}");
            Console.WriteLine(
                $"      order id across two reads: {mta.Id} then {again.Id} " +
                $"({(stable ? "stable" : "reissued per read")}); tool id {mta.ToolId} either way");

            return $"tool {mta.ToolId}, {mta.MtaCurrency ?? "no currency"}, " +
                $"order id {(stable ? "stable" : "not an identity")}";
        });

        // Prefer an alert made for this tool, so a run that can delete does.
        var target = alerts.FirstOrDefault(a => IsSweepAlert(a) && a.Id is not null)
            ?? alerts.FirstOrDefault(a => a.Id is not null);

        if (target?.Id is not { } alertId)
        {
            const string Reason =
                "the account has no alert; IBKR has no endpoint to create one, so make one in TWS";
            probe.Skip("GET  /iserver/account/alert/{id}", Reason);
            probe.Skip("POST /iserver/account/{id}/alert/activate", Reason);
            probe.Skip("DELETE /iserver/account/{id}/alert/{id}", Reason);
            return;
        }

        var disposable = IsSweepAlert(target);
        var wasActive = target.IsActive;

        await probe.RunAsync("GET  /iserver/account/alert/{id}", async () =>
        {
            var details = await ibkr.Alerts.GetDetailsAsync(alertId, cancellationToken);
            wasActive = details.IsActive;

            var conditions = string.Join(
                " / ",
                details.Conditions.Select(c =>
                    $"{c.ContractDescription ?? c.Conidex} {c.Operator} {c.Value}".Trim()));

            Console.WriteLine($"      \"{details.Name}\" {conditions}");
            return $"{details.Conditions.Count} condition(s), {details.TimeInForce}, {details.OrderStatus}";
        });

        if (wasActive is not { } active)
        {
            probe.Skip(
                "POST /iserver/account/{id}/alert/activate",
                "the alert does not report whether it is active, so a no-op cannot be aimed");
        }
        else
        {
            await probe.RunAsync("POST /iserver/account/{id}/alert/activate", async () =>
            {
                // Deliberately re-applies the state the alert is already in. That reaches the
                // endpoint and reads its acknowledgement without arming an alert the user disarmed,
                // or disarming one they are relying on.
                var result = await ibkr.Alerts.SetActiveAsync(account, alertId, active, cancellationToken);
                return $"re-applied active={active}: success={result.Success}, \"{result.Text}\"";
            });
        }

        if (!disposable)
        {
            probe.Skip(
                "DELETE /iserver/account/{id}/alert/{id}",
                $"would destroy a user alert and IBKR cannot recreate it; name one \"{SweepAlertName}\"");
            return;
        }

        await probe.RunAsync("DELETE /iserver/account/{id}/alert/{id}", async () =>
        {
            var result = await ibkr.Alerts.DeleteAsync(account, alertId, cancellationToken);

            // Asked rather than assumed: the acknowledgement says the request was submitted, not
            // that the alert is gone.
            var remaining = await ibkr.Alerts.GetAllAsync(account, cancellationToken);
            var stillThere = remaining.Any(a => a.Id == alertId);
            return $"success={result.Success}; {(stillThere ? "STILL LISTED" : "gone from the listing")}";
        });
    }

    private static bool IsSweepAlert(Trading.Models.Alerts.AlertSummary alert) =>
        string.Equals(alert.Name?.Trim(), SweepAlertName, StringComparison.OrdinalIgnoreCase);
}
