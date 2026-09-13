namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// The topic identifiers IBKR's WebSocket speaks, as named constants.
/// </summary>
/// <remarks>
/// A solicited topic is three characters: <c>s</c> for subscribe, then two identifying the feed. Its
/// unsubscribe twin swaps the <c>s</c> for a <c>u</c>. Unsolicited topics arrive without being
/// asked for and cannot be cancelled. Every topic behind <c>/iserver</c> over REST needs the
/// brokerage session over the socket too.
/// </remarks>
public static class StreamingTopics
{
    /// <summary>Keeps the socket session alive. Sent bare, with no target or parameters.</summary>
    public const string KeepAlive = "tic";

    /// <summary>Unsolicited. A confirmation on connect, then a heartbeat every ten seconds.</summary>
    public const string System = "system";

    /// <summary>Unsolicited. The brokerage session's authentication status, on connect and whenever it changes.</summary>
    public const string AuthenticationStatus = "sts";

    /// <summary>Unsolicited. The accounts the username can reach, on connect and whenever they change.</summary>
    public const string AccountUpdates = "act";

    /// <summary>Unsolicited. Urgent exchange and system bulletins.</summary>
    public const string Bulletins = "blt";

    /// <summary>Unsolicited. Brief notifications about trading activity.</summary>
    public const string Notifications = "ntf";

    /// <summary>Live top-of-book market data for one instrument. Needs the brokerage session.</summary>
    public const string MarketData = "smd";

    /// <summary>Historical bars for one instrument, answered once. Needs the brokerage session.</summary>
    public const string HistoricalMarketData = "smh";

    /// <summary>BookTrader price ladder for one instrument. Needs the brokerage session.</summary>
    public const string PriceLadder = "sbd";

    /// <summary>Live order updates. Needs the brokerage session.</summary>
    public const string OrderUpdates = "sor";

    /// <summary>Live trade updates. Needs the brokerage session.</summary>
    public const string Trades = "str";

    /// <summary>Profit and loss updates.</summary>
    public const string ProfitAndLoss = "spl";

    /// <summary>Account summary updates for one account.</summary>
    public const string AccountSummary = "ssd";

    /// <summary>Account ledger updates for one account.</summary>
    public const string AccountLedger = "sld";
}
