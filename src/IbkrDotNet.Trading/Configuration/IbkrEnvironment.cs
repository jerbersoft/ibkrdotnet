namespace IbkrDotNet.Trading.Configuration;

/// <summary>
/// The Interactive Brokers host a client talks to.
/// </summary>
public enum IbkrEnvironment
{
    /// <summary>
    /// The locally running Client Portal Gateway, <c>https://localhost:5000</c>.
    /// </summary>
    /// <remarks>
    /// The retail path. The gateway is a small Java program that performs the credential exchange in
    /// a browser and proxies authenticated requests on the caller's behalf.
    /// </remarks>
    ClientPortalGateway,

    /// <summary>
    /// Interactive Brokers' production host, <c>https://api.ibkr.com</c>.
    /// </summary>
    /// <remarks>
    /// Reached directly using OAuth 1.0a or OAuth 2.0, with no intermediary gateway.
    /// </remarks>
    Production,

    /// <summary>
    /// Interactive Brokers' sandbox host, <c>https://qa.interactivebrokers.com</c>.
    /// </summary>
    Sandbox,
}

/// <summary>
/// Maps an <see cref="IbkrEnvironment"/> to its base address.
/// </summary>
public static class IbkrEnvironments
{
    /// <summary>The Client Portal Gateway's default base address.</summary>
    public static Uri ClientPortalGateway { get; } = new("https://localhost:5000");

    /// <summary>The production base address.</summary>
    public static Uri Production { get; } = new("https://api.ibkr.com");

    /// <summary>The sandbox base address.</summary>
    public static Uri Sandbox { get; } = new("https://qa.interactivebrokers.com");

    /// <summary>
    /// The path prefix shared by every Trading endpoint. Endpoint paths in this library include it,
    /// so the base address is the bare host.
    /// </summary>
    public const string TradingPathPrefix = "/v1/api";

    /// <summary>Returns the base address for an environment.</summary>
    /// <param name="environment">The environment.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="environment"/> is not a known value.</exception>
    public static Uri GetBaseAddress(IbkrEnvironment environment) => environment switch
    {
        IbkrEnvironment.ClientPortalGateway => ClientPortalGateway,
        IbkrEnvironment.Production => Production,
        IbkrEnvironment.Sandbox => Sandbox,
        _ => throw new ArgumentOutOfRangeException(
            nameof(environment),
            environment,
            "Unknown IBKR environment."),
    };
}
