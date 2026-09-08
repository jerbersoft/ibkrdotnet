using System.ComponentModel.DataAnnotations;
using IbkrDotNet.Trading.Http.RateLimiting;
using NodaTime;

namespace IbkrDotNet.Trading.Configuration;

/// <summary>
/// Configures the Interactive Brokers Trading client.
/// </summary>
public sealed class IbkrTradingOptions
{
    /// <summary>The configuration section this library binds to by convention.</summary>
    public const string SectionName = "Ibkr";

    /// <summary>
    /// The IBKR host to talk to. Ignored when <see cref="BaseAddress"/> is set.
    /// </summary>
    public IbkrEnvironment Environment { get; set; } = IbkrEnvironment.ClientPortalGateway;

    /// <summary>
    /// An explicit base address, overriding <see cref="Environment"/>.
    /// </summary>
    /// <remarks>
    /// Useful when the Client Portal Gateway has been moved off its default port, and in tests.
    /// </remarks>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// The <c>User-Agent</c> sent with every request. IBKR asks that all clients set one.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string UserAgent { get; set; } = DefaultUserAgent;

    /// <summary>The per-request timeout. Defaults to 30 seconds.</summary>
    public Duration Timeout { get; set; } = Duration.FromSeconds(30);

    /// <summary>Rate limiting behaviour.</summary>
    public IbkrRateLimitingOptions RateLimiting { get; set; } = new();

    /// <summary>
    /// The default <c>User-Agent</c>, identifying this library and its version.
    /// </summary>
    public static string DefaultUserAgent { get; } =
        $"IbkrDotNet.Trading/{typeof(IbkrTradingOptions).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"}";

    /// <summary>Resolves the base address this client should use.</summary>
    public Uri ResolveBaseAddress() =>
        BaseAddress ?? IbkrEnvironments.GetBaseAddress(Environment);
}

/// <summary>
/// Configures how the client paces requests against Interactive Brokers' published rate limits.
/// </summary>
public sealed class IbkrRateLimitingOptions
{
    /// <summary>
    /// Whether the client paces requests locally. Enabled by default.
    /// </summary>
    /// <remarks>
    /// Turning this off is rarely a good idea. IBKR responds to a breach by putting the offending IP
    /// address in a ten-minute penalty box, and repeat violators can be blocked outright, so the
    /// cost of exceeding a limit is not paid by the individual request.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to enforce IBKR's global cap of 10 requests per second per authenticated username.
    /// </summary>
    public bool EnforceGlobalLimit { get; set; } = true;

    /// <summary>
    /// The longest the client will pause to stay within a limit before throwing
    /// <see cref="Http.IbkrRateLimitExceededException"/>. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// Several endpoints permit only one request per fifteen minutes. Blocking silently for that
    /// long would be indistinguishable from a hang, so past this threshold the client fails instead.
    /// </remarks>
    public Duration MaxWait { get; set; } = Duration.FromSeconds(30);

    /// <summary>
    /// How long an endpoint is held after IBKR rejects a request to it with
    /// <c>429 Too Many Requests</c> but names no <c>Retry-After</c> and no published limit covers
    /// the path. Defaults to 5 seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where a published limit does cover the path, its own window is used instead of this, on the
    /// grounds that the window is the figure the client already believed and the rejection is only
    /// evidence that it was not applied early enough.
    /// </para>
    /// <para>
    /// This value is a judgement rather than something IBKR documents, and it is the least bad of
    /// two mistakes. Held too briefly, a caller in a loop retries into the same rejection and keeps
    /// doing so, which is precisely the behaviour that gets an address blocked. Held too long, a
    /// single stray <c>429</c> stalls a healthy client -- though only up to
    /// <see cref="MaxWait"/>, past which the caller is told rather than kept waiting.
    /// </para>
    /// </remarks>
    public Duration DefaultRetryAfter { get; set; } = Duration.FromSeconds(5);

    /// <summary>
    /// Extra limits to enforce alongside the published ones, matched before them.
    /// </summary>
    public IList<IbkrRateLimit> AdditionalLimits { get; } = [];
}
