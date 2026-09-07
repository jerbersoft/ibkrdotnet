namespace IbkrDotNet.Trading.Models.MarketData;

/// <summary>
/// The tick identifiers used as the <c>fields</c> parameter of
/// <c>GET /iserver/marketdata/snapshot</c>, and as the keys of the response.
/// </summary>
/// <remarks>
/// IBKR identifies market data points by number rather than by name, and the same numbers key the
/// response object. These constants exist so calling code does not carry bare integers whose meaning
/// has to be looked up.
/// </remarks>
public static class MarketDataField
{
    /// <summary>Last traded price. May carry a prefix such as <c>C</c> for a close or <c>H</c> for a halt.</summary>
    public const string LastPrice = "31";

    /// <summary>Symbol.</summary>
    public const string Symbol = "55";

    /// <summary>Text.</summary>
    public const string Text = "58";

    /// <summary>Current day's high price.</summary>
    public const string High = "70";

    /// <summary>Current day's low price.</summary>
    public const string Low = "71";

    /// <summary>Market value of the position in the instrument.</summary>
    public const string MarketValue = "73";

    /// <summary>Average price of the position.</summary>
    public const string AveragePrice = "74";

    /// <summary>Unrealized profit or loss.</summary>
    public const string UnrealizedPnl = "75";

    /// <summary>Formatted position.</summary>
    public const string FormattedPosition = "76";

    /// <summary>Formatted unrealized profit or loss.</summary>
    public const string FormattedUnrealizedPnl = "77";

    /// <summary>Profit or loss for the day since the prior close.</summary>
    public const string DailyPnl = "78";

    /// <summary>Realized profit or loss.</summary>
    public const string RealizedPnl = "79";

    /// <summary>Unrealized profit or loss, as a percentage.</summary>
    public const string UnrealizedPnlPercent = "80";

    /// <summary>Change from the previous day's close.</summary>
    public const string Change = "82";

    /// <summary>Change from the previous day's close, as a percentage.</summary>
    public const string ChangePercent = "83";

    /// <summary>Highest bid.</summary>
    public const string BidPrice = "84";

    /// <summary>Size offered at the ask.</summary>
    public const string AskSize = "85";

    /// <summary>Lowest offer.</summary>
    public const string AskPrice = "86";

    /// <summary>Volume for the day.</summary>
    public const string Volume = "87";

    /// <summary>Size bid for at the bid.</summary>
    public const string BidSize = "88";

    /// <summary>The option's right: call or put.</summary>
    public const string Right = "201";

    /// <summary>Exchange.</summary>
    public const string Exchange = "6004";

    /// <summary>Contract identifier.</summary>
    public const string ConId = "6008";

    /// <summary>Asset class.</summary>
    public const string SecurityType = "6070";

    /// <summary>Months.</summary>
    public const string Months = "6072";

    /// <summary>Regular expiry.</summary>
    public const string RegularExpiry = "6073";

    /// <summary>
    /// Market data availability, as three characters: R realtime, D delayed, Z frozen, Y frozen
    /// delayed, N not subscribed, P snapshot, p consolidated, B top of book.
    /// </summary>
    public const string MarketDataAvailability = "6509";

    /// <summary>Underlying contract identifier.</summary>
    public const string UnderlyingConId = "6457";

    /// <summary>Company name.</summary>
    public const string CompanyName = "7051";

    /// <summary>Size traded at the last price.</summary>
    public const string LastSize = "7059";

    /// <summary>Today's opening price.</summary>
    public const string Open = "7295";

    /// <summary>Today's closing price.</summary>
    public const string Close = "7296";

    /// <summary>Mark price.</summary>
    public const string Mark = "7635";

    /// <summary>52-week high.</summary>
    public const string FiftyTwoWeekHigh = "7293";

    /// <summary>52-week low.</summary>
    public const string FiftyTwoWeekLow = "7294";

    /// <summary>Average daily trading volume over 90 days.</summary>
    public const string AverageVolume = "7282";

    /// <summary>Option delta.</summary>
    public const string Delta = "7308";

    /// <summary>Option gamma.</summary>
    public const string Gamma = "7309";

    /// <summary>Option theta.</summary>
    public const string Theta = "7310";

    /// <summary>Option vega.</summary>
    public const string Vega = "7311";

    /// <summary>Implied volatility, as a percentage.</summary>
    public const string ImpliedVolatilityPercent = "7633";

    /// <summary>Shares available to short.</summary>
    public const string ShortableShares = "7636";

    /// <summary>Interest rate charged on borrowed shares.</summary>
    public const string FeeRate = "7637";

    /// <summary>Whether the instrument can currently be traded.</summary>
    public const string CanBeTraded = "7184";

    /// <summary>A common set for a top-of-book quote: last, bid, ask and their sizes, plus volume.</summary>
    public static IReadOnlyList<string> TopOfBook { get; } =
        [LastPrice, BidPrice, AskPrice, BidSize, AskSize, Volume];
}
