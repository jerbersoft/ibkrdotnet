using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.PortfolioAnalyst;

/// <summary>
/// An account's transactions in one or more contracts, as returned by
/// <c>POST /pa/transactions</c>.
/// </summary>
/// <remarks>
/// Covers rather more than trades: dividends, payments in lieu and transfers appear alongside buys
/// and sells, distinguished by <see cref="Transaction.Type"/>.
/// </remarks>
public sealed record TransactionHistory
{
    /// <summary>The request identifier, <c>getTransactions</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>IBKR's internal data identifier.</summary>
    /// <remarks>Documented as "Client portal use only"; a live gateway omits it entirely.</remarks>
    [JsonPropertyName("rc")]
    public long? ResponseCode { get; init; }

    /// <summary>Roughly the width of the window, in calendar days.</summary>
    /// <remarks>
    /// A request for 365 days answers <c>366</c>, against seven transactions. See
    /// <see cref="AccountPerformance.DayCount"/>: IBKR calls this the total data points on every
    /// endpoint that sends it, and it is not that on any of them.
    /// </remarks>
    [JsonPropertyName("nd")]
    public long? DayCount { get; init; }

    /// <summary>The currency the amounts are denominated in.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The start of the window the transactions were drawn from.</summary>
    [JsonPropertyName("from")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? From { get; init; }

    /// <summary>The end of the window the transactions were drawn from.</summary>
    [JsonPropertyName("to")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? To { get; init; }

    /// <summary>Whether the transactions are current as of now.</summary>
    /// <remarks>
    /// Documented, but a live gateway omits it and reports the same thing per transaction on
    /// <see cref="Transaction.IsRealTime"/> instead.
    /// </remarks>
    [JsonPropertyName("includesRealTime")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IncludesRealTime { get; init; }

    /// <summary>The realized profit and loss over the same window.</summary>
    [JsonPropertyName("rpnl")]
    public RealizedProfitAndLoss? RealizedProfitAndLoss { get; init; }

    /// <summary>The transactions themselves.</summary>
    [JsonPropertyName("transactions")]
    public IReadOnlyList<Transaction> Transactions { get; init; } = [];
}

/// <summary>
/// Realized profit and loss, broken down by day.
/// </summary>
public sealed record RealizedProfitAndLoss
{
    /// <summary>The realized amounts, one entry per day and contract.</summary>
    /// <remarks>
    /// IBKR's published example puts the literal string <c>"string"</c> in this array where an
    /// object belongs -- a schema placeholder that escaped into the documentation -- so its example
    /// response cannot be deserialized against its own schema. A live gateway sends objects.
    /// </remarks>
    [JsonPropertyName("data")]
    public IReadOnlyList<RealizedProfitAndLossEntry> Data { get; init; } = [];

    /// <summary>The total realized over every day returned.</summary>
    [JsonPropertyName("amt")]
    public decimal? Amount { get; init; }
}

/// <summary>
/// One day's realized profit or loss on one contract.
/// </summary>
public sealed record RealizedProfitAndLossEntry
{
    /// <summary>The day the amount was realized.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The currency the amount is denominated in.</summary>
    [JsonPropertyName("cur")]
    public string? Currency { get; init; }

    /// <summary>The rate the amount was converted at.</summary>
    [JsonPropertyName("fxRate")]
    public decimal? ExchangeRate { get; init; }

    /// <summary>IBKR's <c>side</c> flag.</summary>
    /// <remarks>
    /// Left as text rather than modelled as the gain-or-loss enumeration IBKR documents, because the
    /// documentation does not survive contact with the data. IBKR defines <c>L</c> as LOSS and
    /// <c>G</c> as GAIN; a live gateway reported <c>L</c> on entries whose <see cref="Amount"/> was
    /// <c>+92.71</c> and <c>+20.90</c>, both plainly gains, and set <see cref="PositionSide"/> to
    /// <c>long</c> on the same entries. Read the sign of <see cref="Amount"/> to tell a gain from a
    /// loss.
    /// </remarks>
    [JsonPropertyName("side")]
    public string? Side { get; init; }

    /// <summary>Whether the realized position was long or short.</summary>
    /// <remarks>Undocumented, and sent by a live gateway on every entry.</remarks>
    [JsonPropertyName("positionSide")]
    public string? PositionSide { get; init; }

    /// <summary>The account the amount was realized in.</summary>
    [JsonPropertyName("acctid")]
    public AccountId? AccountId { get; init; }

    /// <summary>The amount realized, positive for a gain.</summary>
    [JsonPropertyName("amt")]
    public decimal? Amount { get; init; }

    /// <summary>The contract the amount was realized on.</summary>
    /// <remarks>
    /// Quoted here and sent as a number by <see cref="Transaction.ConId"/> in the same response.
    /// </remarks>
    [JsonPropertyName("conid")]
    public ConId? ConId { get; init; }
}

/// <summary>
/// One transaction: a trade, a dividend, a payment in lieu or a transfer.
/// </summary>
public sealed record Transaction
{
    /// <summary>The day the transaction settled to.</summary>
    /// <remarks>
    /// Bound from <c>rawDate</c>, which IBKR does not document but does send. The documented
    /// <c>date</c> is the display form and is kept as <see cref="DateText"/>; despite the names, the
    /// undocumented field is the machine-readable one.
    /// </remarks>
    [JsonPropertyName("rawDate")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>IBKR's display form of the date.</summary>
    /// <remarks>
    /// Text rather than a NodaTime value. The format is Java's default rendering of a date --
    /// <c>Wed Nov 05 00:00:00 EST 2025</c> -- whose zone is an abbreviation rather than an
    /// identifier, and abbreviations are ambiguous across the world's zones. <see cref="Date"/>
    /// carries the same day unambiguously, so nothing is lost by leaving this as it arrived.
    /// </remarks>
    [JsonPropertyName("date")]
    public string? DateText { get; init; }

    /// <summary>The currency the transaction was denominated in.</summary>
    [JsonPropertyName("cur")]
    public string? Currency { get; init; }

    /// <summary>The rate the amount was converted at.</summary>
    [JsonPropertyName("fxRate")]
    public decimal? ExchangeRate { get; init; }

    /// <summary>The price per share.</summary>
    /// <remarks>Documented as an integer; a live gateway answers <c>267.71</c>.</remarks>
    [JsonPropertyName("pr")]
    public decimal? Price { get; init; }

    /// <summary>The quantity, negative for a sale.</summary>
    [JsonPropertyName("qty")]
    public decimal? Quantity { get; init; }

    /// <summary>The account that made the transaction.</summary>
    [JsonPropertyName("acctid")]
    public AccountId? AccountId { get; init; }

    /// <summary>The total value, negative for a purchase.</summary>
    /// <remarks>
    /// The sign is the opposite of <see cref="Quantity"/>'s: buying ten shares is a quantity of
    /// <c>+10</c> and an amount of <c>-2677.10</c>, because the cash moved the other way.
    /// </remarks>
    [JsonPropertyName("amt")]
    public decimal? Amount { get; init; }

    /// <summary>The contract transacted.</summary>
    [JsonPropertyName("conid")]
    public ConId? ConId { get; init; }

    /// <summary>What kind of transaction this was, for example <c>Buy</c> or <c>Payment In Lieu</c>.</summary>
    /// <remarks>
    /// Left as text: IBKR describes it only as "the order side", which it is not -- a live gateway
    /// answers <c>Payment In Lieu</c> alongside <c>Buy</c> and <c>Sell</c> -- and never lists the
    /// values.
    /// </remarks>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The contract's long name.</summary>
    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    /// <summary>Whether this transaction is current rather than settled overnight.</summary>
    /// <remarks>Undocumented, and sent by a live gateway on every transaction.</remarks>
    [JsonPropertyName("isRealTime")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? IsRealTime { get; init; }
}
