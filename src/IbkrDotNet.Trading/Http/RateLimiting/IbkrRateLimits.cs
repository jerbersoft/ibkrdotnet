using NodaTime;

namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// One documented Interactive Brokers rate limit.
/// </summary>
/// <param name="PathTemplate">
/// The path the limit applies to. <c>{name}</c> segments match any single segment.
/// </param>
/// <param name="Method">The HTTP method the limit applies to, or <see langword="null"/> for any.</param>
/// <param name="Permits">The number of requests permitted per <paramref name="Window"/>.</param>
/// <param name="Window">The length of the window.</param>
public sealed record IbkrRateLimit(string PathTemplate, string? Method, int Permits, Duration Window);

/// <summary>
/// The rate limits Interactive Brokers publishes for the Trading Web API.
/// </summary>
/// <remarks>
/// <para>
/// IBKR enforces a global cap of 10 requests per second per authenticated username, and a number of
/// endpoints carry their own, much tighter limits on top of that. Exceeding them returns
/// <c>429 Too Many Requests</c>; IBKR may also place the offending IP address in a ten-minute
/// penalty box, and repeat violators can be blocked until the issue is resolved.
/// </para>
/// <para>
/// Because the consequence lands on the IP address rather than the request, the client paces
/// requests locally instead of relying on retries after a rejection.
/// </para>
/// </remarks>
public static class IbkrRateLimits
{
    private const string Prefix = "/v1/api";

    /// <summary>The global limit of 10 requests per second per authenticated username.</summary>
    public static IbkrRateLimit Global { get; } =
        new("/{**any}", null, 10, Duration.FromSeconds(1));

    /// <summary>
    /// The per-endpoint limits, in the order they are matched. The first match wins, so more
    /// specific templates come first.
    /// </summary>
    public static IReadOnlyList<IbkrRateLimit> PerEndpoint { get; } =
    [
        // Market data.
        new($"{Prefix}/iserver/marketdata/snapshot", "GET", 10, Duration.FromSeconds(1)),

        // Scanner: params is the tightest limit in the API and its payload should be cached.
        new($"{Prefix}/iserver/scanner/params", "GET", 1, Duration.FromMinutes(15)),
        new($"{Prefix}/iserver/scanner/run", "POST", 1, Duration.FromSeconds(1)),

        // Orders and executions. IBKR documents these as /iserver/trades and /iserver/orders;
        // the actual resource paths are under /iserver/account.
        new($"{Prefix}/iserver/account/trades", "GET", 1, Duration.FromSeconds(5)),
        new($"{Prefix}/iserver/account/orders", "GET", 1, Duration.FromSeconds(5)),
        new($"{Prefix}/iserver/account/pnl/partitioned", "GET", 1, Duration.FromSeconds(5)),

        // Portfolio.
        new($"{Prefix}/portfolio/accounts", "GET", 1, Duration.FromSeconds(5)),
        new($"{Prefix}/portfolio/subaccounts", "GET", 1, Duration.FromSeconds(5)),

        // PortfolioAnalyst.
        new($"{Prefix}/pa/performance", "POST", 1, Duration.FromMinutes(15)),
        new($"{Prefix}/pa/summary", "POST", 1, Duration.FromMinutes(15)),
        new($"{Prefix}/pa/transactions", "POST", 1, Duration.FromMinutes(15)),
        new($"{Prefix}/pa/allocation", "POST", 1, Duration.FromMinutes(15)),

        // FYIs and notifications: every endpoint in the group is one request per second.
        new($"{Prefix}/fyi/unreadnumber", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/settings", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/settings/{{typecode}}", "POST", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/disclaimer/{{typecode}}", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/disclaimer/{{typecode}}", "PUT", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/deliveryoptions", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/deliveryoptions/email", "PUT", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/deliveryoptions/device", "POST", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/deliveryoptions/{{deviceId}}", "DELETE", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/notifications", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/notifications/more", "GET", 1, Duration.FromSeconds(1)),
        new($"{Prefix}/fyi/notifications/{{notificationId}}", "PUT", 1, Duration.FromSeconds(1)),

        // Session.
        new($"{Prefix}/tickle", null, 1, Duration.FromSeconds(1)),
        new($"{Prefix}/sso/validate", "GET", 1, Duration.FromMinutes(1)),
    ];
}
