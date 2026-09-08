using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Models.Accounts;

/// <summary>The response from <c>GET /iserver/account/search/{searchPattern}</c>.</summary>
public sealed record DynamicAccountSearchResult
{
    /// <summary>The accounts matching the pattern.</summary>
    [JsonPropertyName("matchedAccounts")]
    public IReadOnlyList<MatchedAccount> MatchedAccounts { get; init; } = [];

    /// <summary>The pattern that was searched for.</summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }
}

/// <summary>An account matched by a dynamic account search.</summary>
public sealed record MatchedAccount
{
    /// <summary>The account identifier.</summary>
    [JsonPropertyName("accountId")]
    public AccountId AccountId { get; init; }

    /// <summary>The account's display alias.</summary>
    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    /// <summary>The allocation identifier.</summary>
    [JsonPropertyName("allocationId")]
    public string? AllocationId { get; init; }
}

/// <summary>The response from switching the selected or active account.</summary>
public sealed record SetAccountResponse
{
    /// <summary>Whether the account was switched.</summary>
    [JsonPropertyName("set")]
    public bool Set { get; init; }

    /// <summary>The account now selected.</summary>
    [JsonPropertyName("acctId")]
    public AccountId? AccountId { get; init; }
}

/// <summary>The response from <c>GET /acesws/{accountId}/signatures-and-owners</c>.</summary>
public sealed record AccountOwners
{
    /// <summary>The account identifier.</summary>
    [JsonPropertyName("accountId")]
    public AccountId AccountId { get; init; }

    /// <summary>The users associated with the account.</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<AccountUser> Users { get; init; } = [];

    /// <summary>The applicant details, including signatures.</summary>
    [JsonPropertyName("applicant")]
    public AccountApplicant? Applicant { get; init; }
}

/// <summary>A user associated with an account.</summary>
public sealed record AccountUser
{
    /// <summary>The user's role, for example <c>OWNER</c>.</summary>
    [JsonPropertyName("roleId")]
    public string? RoleId { get; init; }

    /// <summary>Whether the user holds the right code indicator.</summary>
    [JsonPropertyName("hasRightCodeInd")]
    public bool? HasRightCodeIndicator { get; init; }

    /// <summary>The username.</summary>
    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    /// <summary>The legal entity behind the user.</summary>
    [JsonPropertyName("entity")]
    public AccountEntity? Entity { get; init; }
}

/// <summary>The legal entity behind an account user.</summary>
public sealed record AccountEntity
{
    /// <summary>The entity's name.</summary>
    [JsonPropertyName("entityName")]
    public string? EntityName { get; init; }

    /// <summary>The entity type, for example <c>INDIVIDUAL</c>.</summary>
    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    /// <summary>The first name, for an individual.</summary>
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    /// <summary>The last name, for an individual.</summary>
    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }
}

/// <summary>Applicant details for an account.</summary>
public sealed record AccountApplicant
{
    /// <summary>The signatures on the account.</summary>
    [JsonPropertyName("signatures")]
    public IReadOnlyList<string> Signatures { get; init; } = [];
}
