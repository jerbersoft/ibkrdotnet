using System.Net;
using IbkrDotNet.Trading;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Notifications;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Samples.Verify;

/// <summary>
/// The FYI and notification endpoints: four reads run, seven writes deliberately do not.
/// </summary>
/// <remarks>
/// <para>
/// The four reads run on every sweep. The seven writes never do, and there is no flag to turn them
/// on. Every other write path in this sweep undoes itself -- the watchlist is deleted, the order is
/// cancelled, the share is sold back -- but these change delivery settings and subscriptions on the
/// username, and IBKR documents no way to read the previous value before overwriting it or to
/// restore a device once it is gone. A verification tool that leaves the user's notification
/// settings different from how it found them has done more than verify.
/// </para>
/// <para>
/// Marking things read has the same problem in a quieter way: <c>PUT /fyi/disclaimer/{typecode}</c>
/// records the user's acknowledgement of a legal notice, and <c>PUT /fyi/notifications/{id}</c>
/// documents no parameter for setting a message back to unread. Both are one-way.
/// </para>
/// <para>
/// So the writes are exercised by the unit tests against IBKR's documented payloads and are listed
/// here as deliberate skips, each saying what it would have changed. The disclaimer read is the
/// interesting live check: it is driven by a type code the settings call actually returned, rather
/// than a hard-coded one, so a run also proves the two endpoints agree about what a category is.
/// </para>
/// </remarks>
internal static class NotificationChecks
{
    /// <summary>How many notifications to ask for. Enough to see a date, not enough to be slow.</summary>
    private const long PageSize = 10;

    /// <summary>How many times to re-ask an endpoint that answers that it is not ready yet.</summary>
    private const int WarmUpAttempts = 6;

    /// <summary>
    /// The wait between those attempts. Every endpoint in this group is limited to one request per
    /// second, so this is the floor rather than a choice.
    /// </summary>
    private static readonly TimeSpan WarmUpInterval = TimeSpan.FromSeconds(2);

    public static async Task RunAsync(
        IIbkrTradingClient ibkr,
        Probe probe,
        CancellationToken cancellationToken)
    {
        probe.Group("FYIs and notifications");

        await probe.RunAsync("GET  /fyi/unreadnumber", async () =>
        {
            var (unread, attempts) = await WarmUpAsync(
                () => ibkr.Notifications.GetUnreadCountAsync(cancellationToken), cancellationToken);

            // Absent is not zero, and the client keeps them apart, so the report should too.
            var count = unread is { } value ? $"{value} unread" : "IBKR returned no count";
            return attempts == 1 ? count : $"{count} (after {attempts} attempts)";
        });

        await probe.RunAsync("GET  /fyi/notifications", async () =>
        {
            var (notifications, attempts) = await WarmUpAsync(
                () => ibkr.Notifications.GetAllAsync(PageSize, cancellationToken: cancellationToken),
                cancellationToken);

            if (notifications.Count == 0)
            {
                return "none on this username";
            }

            // The date is the field worth printing. IBKR sends it as a quoted epoch with a
            // fractional part -- "1710847062.0" -- which is a shape no other endpoint uses.
            var newest = notifications[0];
            Console.WriteLine($"      newest  {newest.Date}  {newest.TypeCode}  {newest.Title}");

            var dated = notifications.Count(n => n.Date is not null);
            var unread = notifications.Count(n => n.IsRead is false);
            var summary = $"{notifications.Count} returned, {dated} with a date, {unread} unread";
            return attempts == 1 ? summary : $"{summary} (after {attempts} attempts)";
        });

        IReadOnlyList<NotificationSetting> settings = [];
        await probe.RunAsync("GET  /fyi/settings", async () =>
        {
            settings = await ibkr.Notifications.GetSettingsAsync(cancellationToken);

            // Whether the live codes stay inside IBKR's documented set of twenty-three is the point
            // of the type code being an open struct rather than an enum. IBKR's own example already
            // breaks its own list, so a run that finds more is confirmation, not a surprise.
            var undocumented = settings
                .Select(s => s.TypeCode)
                .Where(code => code is { } value && !Documented.Contains(value))
                .Select(code => code!.Value.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (undocumented.Length > 0)
            {
                Console.WriteLine(
                    $"      codes outside IBKR's documented set: {string.Join(", ", undocumented)}");
            }

            var changeable = settings.Count(s => s.IsChangeable is true);
            return $"{settings.Count} categor(ies), {changeable} changeable, " +
                $"{undocumented.Length} undocumented code(s)";
        });

        await probe.RunAsync("GET  /fyi/deliveryoptions", async () =>
        {
            var options = await ibkr.Notifications.GetDeliveryOptionsAsync(cancellationToken);

            // Device identifiers are push tokens, so the count is reported and the values are not.
            var enabled = options.Devices.Count(d => d.IsEnabled is true);
            return $"email {(options.IsEmailEnabled is true ? "on" : "off")}, " +
                $"{options.Devices.Count} device(s), {enabled} enabled";
        });

        // Driven by what the settings call returned rather than a hard-coded code, so this also
        // shows the two endpoints agree about what a category is.
        if (settings.Select(s => s.TypeCode).FirstOrDefault(code => code is not null) is not { } typeCode)
        {
            probe.Skip("GET  /fyi/disclaimer/{typecode}", "the settings carried no type code to ask about");
        }
        else
        {
            await probe.RunAsync("GET  /fyi/disclaimer/{typecode}", async () =>
            {
                var disclaimer = await ibkr.Notifications.GetDisclaimerAsync(typeCode, cancellationToken);
                var text = disclaimer.Text;

                return text is { Length: > 0 }
                    ? $"{typeCode}: {text.Length} character(s), echoed code {disclaimer.TypeCode}"
                    : $"{typeCode}: no disclaimer text";
            });
        }

        SkipTheWrites(probe);
    }

    /// <summary>
    /// Calls an endpoint that may answer that it is not ready yet, and reports how many attempts it
    /// took.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Undocumented behaviour, found by this sweep. Two endpoints in this group answer a cold call
    /// with <c>503 Service Unavailable</c> or with <c>423 Locked</c> and the body
    /// <c>{"status":"waiting for reply"}</c>, and then serve the data once the gateway has fetched
    /// it. IBKR documents neither status on any endpoint here.
    /// </para>
    /// <para>
    /// The retry lives in the sweep rather than in the client on purpose. A library that silently
    /// re-sent requests to an endpoint limited to one per second would be spending the caller's rate
    /// budget on a condition it cannot see, and the penalty for exhausting that budget lands on the
    /// IP address. <see cref="IbkrApiException.StatusCode"/> is right there for a caller who wants
    /// this policy; it should be theirs to choose.
    /// </para>
    /// </remarks>
    private static async Task<(T Value, int Attempts)> WarmUpAsync<T>(
        Func<Task<T>> call,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return (await call(), attempt);
            }
            catch (IbkrApiException ex) when (IsNotReadyYet(ex) && attempt < WarmUpAttempts)
            {
                await Task.Delay(WarmUpInterval, cancellationToken);
            }
            catch (IbkrApiException ex) when (IsNotReadyYet(ex))
            {
                // Not a failure of this library: the gateway said it was still waiting on IBKR, and
                // kept saying it. Reported as a skip so the run does not blame the client for it.
                throw new SkipCheckException(
                    $"the gateway answered {(int?)ex.StatusCode} on all {WarmUpAttempts} attempts, " +
                    "which is how it says it is still waiting on IBKR");
            }
        }
    }

    /// <summary>Whether IBKR's answer means "ask again shortly" rather than "no".</summary>
    private static bool IsNotReadyYet(IbkrApiException ex) =>
        ex.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.Locked;

    /// <summary>
    /// Lists the writes the sweep will not perform, each with what it would have changed.
    /// </summary>
    /// <remarks>
    /// Listed rather than silently omitted, so the report shows the whole group and says why five
    /// sixths of it is untested live. A skip that names its reason is a finding; an absent line is
    /// an oversight.
    /// </remarks>
    private static void SkipTheWrites(Probe probe)
    {
        const string Reason = "changes settings on the username and does not undo itself";

        probe.Skip("PUT  /fyi/notifications/{id}", $"{Reason}; marks a message read, with no way back");
        probe.Skip("POST /fyi/settings/{typecode}", $"{Reason}; subscribes or unsubscribes a category");
        probe.Skip("PUT  /fyi/disclaimer/{typecode}", $"{Reason}; records acceptance of a legal notice");
        probe.Skip("POST /fyi/deliveryoptions/device", $"{Reason}; toggles push to a real device");
        probe.Skip("PUT  /fyi/deliveryoptions/email", $"{Reason}; toggles the account's email delivery");
        probe.Skip("DELETE /fyi/deliveryoptions/{id}", "deletes a device IBKR offers no way to re-register");
    }

    /// <summary>The twenty-three type codes IBKR's reference lists.</summary>
    /// <remarks>
    /// Held here rather than on <see cref="NotificationTypeCode"/> so the library carries no opinion
    /// about which codes are real. This is the sweep's yardstick for reporting what the live gateway
    /// returns beyond the documentation, which is the only reason the set is worth writing down.
    /// </remarks>
    private static readonly HashSet<NotificationTypeCode> Documented =
    [
        NotificationTypeCode.BorrowAvailability,
        NotificationTypeCode.ComparableAlgo,
        NotificationTypeCode.DividendsAdvisory,
        NotificationTypeCode.UpcomingEarnings,
        NotificationTypeCode.MutualFundAdvisory,
        NotificationTypeCode.OptionExpiration,
        NotificationTypeCode.PortfolioBuilderRebalance,
        NotificationTypeCode.SuspendOrderOnEconomicEvent,
        NotificationTypeCode.ShortTermGainTurningLongTerm,
        NotificationTypeCode.SystemMessages,
        NotificationTypeCode.AssignmentRealizingLongTermGains,
        NotificationTypeCode.Takeover,
        NotificationTypeCode.UserAlert,
        NotificationTypeCode.M871Trades,
        NotificationTypeCode.PlatformUseSuggestions,
        NotificationTypeCode.UnexercisedOptionLossPrevention,
        NotificationTypeCode.PositionTransfer,
        NotificationTypeCode.MissingCostBasis,
        NotificationTypeCode.Milestones,
        NotificationTypeCode.DepreciationNotice,
        NotificationTypeCode.SaveTaxes,
        NotificationTypeCode.TradeIdea,
        NotificationTypeCode.CashTransfer,
    ];
}
