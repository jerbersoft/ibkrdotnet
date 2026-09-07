using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Portfolio;

/// <summary>
/// An account whose positions and balances the username may view.
/// </summary>
/// <remarks>
/// Returned by <c>/portfolio/accounts</c>, <c>/portfolio/subaccounts</c> and
/// <c>/portfolio/{accountId}/meta</c>. These are viewable accounts, which are not necessarily the
/// same set as the tradable accounts from <c>/iserver/accounts</c>.
/// </remarks>
public sealed record PortfolioAccount
{
    /// <summary>The account identifier.</summary>
    [JsonPropertyName("accountId")]
    public AccountId AccountId { get; init; }

    /// <summary>The account identifier, repeated by IBKR under a second key.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The account's user-assigned alias.</summary>
    [JsonPropertyName("accountAlias")]
    public string? AccountAlias { get; init; }

    /// <summary>The account title.</summary>
    [JsonPropertyName("accountTitle")]
    public string? AccountTitle { get; init; }

    /// <summary>The account's virtual account number.</summary>
    [JsonPropertyName("accountVan")]
    public string? AccountVan { get; init; }

    /// <summary>The display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>The account description.</summary>
    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    /// <summary>When the account was opened.</summary>
    [JsonPropertyName("accountStatus")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? OpenedAt { get; init; }

    /// <summary>The customer type, for example <c>LLC</c>.</summary>
    [JsonPropertyName("acctCustType")]
    public string? CustomerType { get; init; }

    /// <summary>The account's base currency.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>The IBKR entity the account is held with, for example <c>IBLLC-US</c>.</summary>
    [JsonPropertyName("ibEntity")]
    public string? IbEntity { get; init; }

    /// <summary>The business type.</summary>
    [JsonPropertyName("businessType")]
    public string? BusinessType { get; init; }

    /// <summary>The clearing status.</summary>
    [JsonPropertyName("clearingStatus")]
    public string? ClearingStatus { get; init; }

    /// <summary>The trading permissions, for example <c>STKNOPT</c>.</summary>
    [JsonPropertyName("tradingType")]
    public string? TradingType { get; init; }

    /// <summary>The account type, for example <c>DEMO</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The account category.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Whether the account has brokerage access.</summary>
    [JsonPropertyName("brokerageAccess")]
    public bool? BrokerageAccess { get; init; }

    /// <summary>Whether client trading is disallowed.</summary>
    [JsonPropertyName("noClientTrading")]
    public bool? NoClientTrading { get; init; }

    /// <summary>Whether a virtual foreign exchange portfolio is tracked.</summary>
    [JsonPropertyName("trackVirtualFXPortfolio")]
    public bool? TrackVirtualFxPortfolio { get; init; }

    /// <summary>Whether the account is a financial advisor client.</summary>
    [JsonPropertyName("faclient")]
    public bool? IsFinancialAdvisorClient { get; init; }

    /// <summary>Parent account details, for accounts in a multi-level structure.</summary>
    [JsonPropertyName("parent")]
    public PortfolioAccountParent? Parent { get; init; }
}

/// <summary>Parent account details for an account in a multi-level structure.</summary>
public sealed record PortfolioAccountParent
{
    /// <summary>The parent account identifier.</summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; init; }

    /// <summary>Whether the account is a multiplex child.</summary>
    [JsonPropertyName("isMChild")]
    public bool? IsMultiplexChild { get; init; }

    /// <summary>Whether the account is a multiplex parent.</summary>
    [JsonPropertyName("isMParent")]
    public bool? IsMultiplexParent { get; init; }

    /// <summary>Whether the account is multiplexed.</summary>
    [JsonPropertyName("isMultiplex")]
    public bool? IsMultiplex { get; init; }

    /// <summary>Money manager client identifiers.</summary>
    [JsonPropertyName("mmc")]
    public IReadOnlyList<string> MoneyManagerClients { get; init; } = [];
}

/// <summary>The response from <c>GET /portfolio/subaccounts2</c>.</summary>
/// <remarks>
/// Used in place of <c>/portfolio/subaccounts</c> for structures with more than 100 subaccounts.
/// </remarks>
public sealed record SubaccountsPage
{
    /// <summary>Paging details.</summary>
    [JsonPropertyName("metadata")]
    public SubaccountsPageMetadata? Metadata { get; init; }

    /// <summary>The subaccounts on this page.</summary>
    [JsonPropertyName("subaccounts")]
    public IReadOnlyList<PortfolioAccount> Subaccounts { get; init; } = [];
}

/// <summary>Paging details for a page of subaccounts.</summary>
public sealed record SubaccountsPageMetadata
{
    /// <summary>The zero-based page number.</summary>
    [JsonPropertyName("pageNum")]
    public int? PageNumber { get; init; }

    /// <summary>The page size.</summary>
    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    /// <summary>The total number of subaccounts.</summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }
}
