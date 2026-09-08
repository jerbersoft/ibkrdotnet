using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Models.Accounts;

/// <summary>The response from <c>GET /iserver/accounts</c>.</summary>
/// <remarks>
/// These are the accounts the username may <em>trade</em>. For the accounts whose positions and
/// balances may be <em>viewed</em>, see <c>GET /portfolio/accounts</c>; the two sets are not always
/// the same.
/// </remarks>
public sealed record TradableAccounts
{
    /// <summary>Every accessible account identifier.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<AccountId> Accounts { get; init; } = [];

    /// <summary>Per-account trading properties, keyed by account identifier.</summary>
    [JsonPropertyName("acctProps")]
    public IReadOnlyDictionary<string, AccountProperties>? AccountProperties { get; init; }

    /// <summary>Display aliases, keyed by account identifier.</summary>
    [JsonPropertyName("aliases")]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }

    /// <summary>Feature flags for the session.</summary>
    [JsonPropertyName("allowFeatures")]
    public IReadOnlyDictionary<string, object>? AllowFeatures { get; init; }

    /// <summary>Chart periods available per asset class. Internal use.</summary>
    [JsonPropertyName("chartPeriods")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ChartPeriods { get; init; }

    /// <summary>Financial Advisor groups.</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Financial Advisor allocation profiles.</summary>
    [JsonPropertyName("profiles")]
    public IReadOnlyList<string> Profiles { get; init; } = [];

    /// <summary>The account currently selected for trading.</summary>
    [JsonPropertyName("selectedAccount")]
    public AccountId? SelectedAccount { get; init; }

    /// <summary>IBKR server details. Internal use.</summary>
    [JsonPropertyName("serverInfo")]
    public BrokerageServerInfo? ServerInfo { get; init; }

    /// <summary>The session identifier.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>Whether the session is a free trial.</summary>
    [JsonPropertyName("isFt")]
    public bool? IsFreeTrial { get; init; }

    /// <summary>Whether the accounts are paper trading accounts.</summary>
    [JsonPropertyName("isPaper")]
    public bool? IsPaper { get; init; }
}

/// <summary>Trading properties for one account.</summary>
public sealed record AccountProperties
{
    /// <summary>Whether the account has subaccounts.</summary>
    [JsonPropertyName("hasChildAccounts")]
    public bool HasChildAccounts { get; init; }

    /// <summary>Whether the account supports cash quantity orders.</summary>
    [JsonPropertyName("supportsCashQty")]
    public bool SupportsCashQuantity { get; init; }

    /// <summary>Whether the account supports fractional share orders.</summary>
    [JsonPropertyName("supportsFractions")]
    public bool SupportsFractions { get; init; }

    /// <summary>Whether the account is IBKR Lite operating under Pro.</summary>
    [JsonPropertyName("liteUnderPro")]
    public bool LiteUnderPro { get; init; }

    /// <summary>Whether the account is a proprietary trading account.</summary>
    [JsonPropertyName("isProp")]
    public bool? IsProprietary { get; init; }

    /// <summary>Whether orders may specify a customer-supplied time.</summary>
    [JsonPropertyName("allowCustomerTime")]
    public bool AllowCustomerTime { get; init; }

    /// <summary>Whether automatic currency conversion is enabled.</summary>
    [JsonPropertyName("autoFx")]
    public bool AutoFx { get; init; }

    /// <summary>Whether currency conversion is suppressed.</summary>
    /// <remarks>IBKR spells this key both <c>noFxConv</c> and <c>noFXConv</c>; matching is case-insensitive.</remarks>
    [JsonPropertyName("noFxConv")]
    public bool NoFxConversion { get; init; }
}
