using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Portfolio;

/// <summary>
/// Cash and market value for one currency, from <c>GET /portfolio/{accountId}/ledger</c>.
/// </summary>
/// <remarks>
/// The response is keyed by currency code, with <c>BASE</c> holding the account's base-currency
/// totals.
/// </remarks>
public sealed record LedgerEntry
{
    /// <summary>The account this ledger belongs to.</summary>
    [JsonPropertyName("acctcode")]
    public string? AccountCode { get; init; }

    /// <summary>The currency, or <c>BASE</c> for the account's base currency.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>
    /// When this ledger data was retrieved.
    /// </summary>
    /// <remarks>
    /// Epoch <em>seconds</em> here, unlike most other IBKR timestamps, which are milliseconds.
    /// </remarks>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(InstantEpochSecondsConverter))]
    public Instant? RetrievedAt { get; init; }

    /// <summary>The cash balance.</summary>
    [JsonPropertyName("cashbalance")]
    public decimal? CashBalance { get; init; }

    /// <summary>The cash balance in the foreign exchange segment.</summary>
    [JsonPropertyName("cashbalancefxsegment")]
    public decimal? CashBalanceFxSegment { get; init; }

    /// <summary>Settled cash.</summary>
    [JsonPropertyName("settledcash")]
    public decimal? SettledCash { get; init; }

    /// <summary>Net liquidation value.</summary>
    [JsonPropertyName("netliquidationvalue")]
    public decimal? NetLiquidationValue { get; init; }

    /// <summary>The exchange rate to the account's base currency.</summary>
    [JsonPropertyName("exchangerate")]
    public decimal? ExchangeRate { get; init; }

    /// <summary>Realized profit and loss.</summary>
    [JsonPropertyName("realizedpnl")]
    public decimal? RealizedPnl { get; init; }

    /// <summary>Unrealized profit and loss.</summary>
    [JsonPropertyName("unrealizedpnl")]
    public decimal? UnrealizedPnl { get; init; }

    /// <summary>Profit and loss on futures only.</summary>
    [JsonPropertyName("futuresonlypnl")]
    public decimal? FuturesOnlyPnl { get; init; }

    /// <summary>Accrued interest.</summary>
    [JsonPropertyName("interest")]
    public decimal? Interest { get; init; }

    /// <summary>Accrued dividends.</summary>
    [JsonPropertyName("dividends")]
    public decimal? Dividends { get; init; }

    /// <summary>Stock market value.</summary>
    [JsonPropertyName("stockmarketvalue")]
    public decimal? StockMarketValue { get; init; }

    /// <summary>Stock option market value.</summary>
    [JsonPropertyName("stockoptionmarketvalue")]
    public decimal? StockOptionMarketValue { get; init; }

    /// <summary>Futures market value.</summary>
    [JsonPropertyName("futuremarketvalue")]
    public decimal? FutureMarketValue { get; init; }

    /// <summary>Futures option market value.</summary>
    [JsonPropertyName("futureoptionmarketvalue")]
    public decimal? FutureOptionMarketValue { get; init; }

    /// <summary>Commodity market value.</summary>
    [JsonPropertyName("commoditymarketvalue")]
    public decimal? CommodityMarketValue { get; init; }

    /// <summary>Corporate bond market value.</summary>
    [JsonPropertyName("corporatebondsmarketvalue")]
    public decimal? CorporateBondsMarketValue { get; init; }

    /// <summary>Treasury bill market value.</summary>
    [JsonPropertyName("tbillsmarketvalue")]
    public decimal? TreasuryBillsMarketValue { get; init; }

    /// <summary>Treasury bond market value.</summary>
    [JsonPropertyName("tbondsmarketvalue")]
    public decimal? TreasuryBondsMarketValue { get; init; }

    /// <summary>Issuer option market value.</summary>
    [JsonPropertyName("issueroptionsmarketvalue")]
    public decimal? IssuerOptionsMarketValue { get; init; }

    /// <summary>Warrant market value.</summary>
    [JsonPropertyName("warrantsmarketvalue")]
    public decimal? WarrantsMarketValue { get; init; }

    /// <summary>Cryptocurrency value.</summary>
    [JsonPropertyName("cryptocurrencyvalue")]
    public decimal? CryptocurrencyValue { get; init; }

    /// <summary>Money market fund value.</summary>
    [JsonPropertyName("moneyfunds")]
    public decimal? MoneyFunds { get; init; }

    /// <summary>Mutual fund value.</summary>
    [JsonPropertyName("funds")]
    public decimal? Funds { get; init; }

    /// <summary>The message severity. Internal use.</summary>
    [JsonPropertyName("severity")]
    public int? Severity { get; init; }

    /// <summary>The session identifier. Internal use.</summary>
    [JsonPropertyName("sessionid")]
    public long? SessionId { get; init; }
}
