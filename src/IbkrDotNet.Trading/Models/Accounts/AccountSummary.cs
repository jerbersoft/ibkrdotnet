using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Accounts;

/// <summary>The response from <c>GET /iserver/account/{accountId}/summary</c>.</summary>
public sealed record AccountSummary
{
    /// <summary>The account type. Empty for a standard individual account.</summary>
    [JsonPropertyName("accountType")]
    public string? AccountType { get; init; }

    /// <summary>A status message when the account is not currently tradeable.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The total account balance.</summary>
    [JsonPropertyName("balance")]
    public decimal? Balance { get; init; }

    /// <summary>The account's Special Memorandum Account value.</summary>
    [JsonPropertyName("SMA")]
    public decimal? Sma { get; init; }

    /// <summary>Total buying power available.</summary>
    [JsonPropertyName("buyingPower")]
    public decimal? BuyingPower { get; init; }

    /// <summary>Equity available for trading: equity with loan value less initial margin.</summary>
    [JsonPropertyName("availableFunds")]
    public decimal? AvailableFunds { get; init; }

    /// <summary>Cash held in excess of the usual requirement.</summary>
    [JsonPropertyName("excessLiquidity")]
    public decimal? ExcessLiquidity { get; init; }

    /// <summary>The basis for pricing the assets in the account.</summary>
    [JsonPropertyName("netLiquidationValue")]
    public decimal? NetLiquidationValue { get; init; }

    /// <summary>Equity with loan value.</summary>
    [JsonPropertyName("equityWithLoanValue")]
    public decimal? EquityWithLoanValue { get; init; }

    /// <summary>The Regulation T loan amount.</summary>
    [JsonPropertyName("regTLoan")]
    public decimal? RegTLoan { get; init; }

    /// <summary>Gross position value of securities.</summary>
    [JsonPropertyName("securitiesGVP")]
    public decimal? SecuritiesGrossPositionValue { get; init; }

    /// <summary>Cash recognized at the time of trade, plus futures profit and loss.</summary>
    [JsonPropertyName("totalCashValue")]
    public decimal? TotalCashValue { get; init; }

    /// <summary>Interest accrued since the previous coupon date.</summary>
    [JsonPropertyName("accruedInterest")]
    public decimal? AccruedInterest { get; init; }

    /// <summary>Initial margin under Regulation T.</summary>
    [JsonPropertyName("regTMargin")]
    public decimal? RegTMargin { get; init; }

    /// <summary>Available initial margin.</summary>
    [JsonPropertyName("initialMargin")]
    public decimal? InitialMargin { get; init; }

    /// <summary>Available maintenance margin.</summary>
    [JsonPropertyName("maintenanceMargin")]
    public decimal? MaintenanceMargin { get; init; }

    /// <summary>Balances for every currency held.</summary>
    [JsonPropertyName("cashBalances")]
    public IReadOnlyList<CurrencyBalance> CashBalances { get; init; } = [];
}

/// <summary>An account balance in one currency.</summary>
public sealed record CurrencyBalance
{
    /// <summary>
    /// The currency. The base currency total is reported as <c>Total (in {BaseCurrency})</c>.
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>Total currency held.</summary>
    [JsonPropertyName("balance")]
    public decimal? Balance { get; init; }

    /// <summary>Settled cash available for withdrawal.</summary>
    [JsonPropertyName("settledCash")]
    public decimal? SettledCash { get; init; }
}
