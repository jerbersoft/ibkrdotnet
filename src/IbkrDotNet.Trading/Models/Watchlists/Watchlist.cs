using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Watchlists;

/// <summary>
/// A watchlist as it appears in the list from <c>GET /iserver/watchlists</c>.
/// </summary>
/// <remarks>
/// The listing carries only the watchlist's identity and metadata. Its instruments come from
/// <c>GET /iserver/watchlist</c>, modelled by <see cref="Watchlist"/>.
/// </remarks>
public sealed record WatchlistSummary
{
    /// <summary>The watchlist identifier, used to read or delete it.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The display name shown in Trader Workstation and Client Portal.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>When the watchlist was last modified.</summary>
    [JsonPropertyName("modified")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? ModifiedAt { get; init; }

    /// <summary>
    /// Whether the watchlist is write-restricted. Watchlists IBKR creates are read-only.
    /// </summary>
    /// <remarks>
    /// IBKR spells this <c>read_only</c> here and <c>readOnly</c> on
    /// <see cref="Watchlist.IsReadOnly"/>, for the same concept on the same resource.
    /// </remarks>
    [JsonPropertyName("read_only")]
    public bool? IsReadOnly { get; init; }

    /// <summary>Whether the watchlist is currently open in a Client Portal window.</summary>
    [JsonPropertyName("is_open")]
    public bool? IsOpen { get; init; }

    /// <summary>The entry's kind. Always <c>watchlist</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>
/// A single watchlist and its instruments, from <c>GET /iserver/watchlist</c>.
/// </summary>
public sealed record Watchlist
{
    /// <summary>The watchlist identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>IBKR's internal hash of the watchlist.</summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    /// <summary>Whether the watchlist is write-restricted.</summary>
    [JsonPropertyName("readOnly")]
    public bool? IsReadOnly { get; init; }

    /// <summary>The instruments the watchlist holds.</summary>
    [JsonPropertyName("instruments")]
    public IReadOnlyList<WatchlistInstrument> Instruments { get; init; } = [];
}

/// <summary>One instrument held in a watchlist.</summary>
public sealed record WatchlistInstrument
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier again, as a string, under IBKR's shorthand key.</summary>
    /// <remarks>
    /// The same value as <see cref="ConId"/>. It is the key a watchlist row is *written* with, which
    /// is why IBKR echoes it back.
    /// </remarks>
    [JsonPropertyName("C")]
    public string? ConIdText { get; init; }

    /// <summary>The asset class, under IBKR's shorthand key, for example <c>STK</c>.</summary>
    [JsonPropertyName("ST")]
    public string? SecurityType { get; init; }

    /// <summary>The asset class. The same value as <see cref="SecurityType"/>.</summary>
    [JsonPropertyName("assetClass")]
    public string? AssetClass { get; init; }

    /// <summary>The instrument's full display name, for example <c>INTL BUSINESS MACHINES CORP</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The instrument's symbol as displayed, for example <c>IBM</c>.</summary>
    [JsonPropertyName("fullName")]
    public string? FullName { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The instrument name rendered in Chinese, where IBKR has one.</summary>
    [JsonPropertyName("chineseName")]
    public string? ChineseName { get; init; }
}

/// <summary>The outcome of <c>DELETE /iserver/watchlist</c>.</summary>
public sealed record WatchlistDeletion
{
    /// <summary>The identifier of the watchlist IBKR reports having deleted.</summary>
    public string? DeletedId { get; init; }
}

/// <summary>The body of <c>POST /iserver/watchlist</c>.</summary>
/// <param name="Id">The identifier to create the watchlist under. Digits only, and unique.</param>
/// <param name="Name">The display name.</param>
/// <param name="Rows">One row per instrument.</param>
internal sealed record CreateWatchlistRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("rows")] IReadOnlyList<CreateWatchlistRow> Rows);

/// <summary>One instrument row in a watchlist creation request.</summary>
/// <param name="ConId">The instrument's contract identifier, as a string.</param>
public sealed record CreateWatchlistRow(
    [property: JsonPropertyName("C")] string ConId);

/// <summary>
/// The envelope <c>GET /iserver/watchlists</c> wraps its results in.
/// </summary>
/// <remarks>
/// Not part of the public surface. Alongside the list it carries <c>action</c> and <c>MID</c>, both
/// documented as internal use, and two flags describing how Client Portal should render the result.
/// None of that concerns a caller, so the client unwraps it.
/// </remarks>
internal sealed record WatchlistsResponse
{
    [JsonPropertyName("data")]
    public WatchlistsResponseData? Data { get; init; }
}

internal sealed record WatchlistsResponseData
{
    [JsonPropertyName("user_lists")]
    public IReadOnlyList<WatchlistSummary> UserLists { get; init; } = [];
}

/// <summary>The envelope <c>DELETE /iserver/watchlist</c> wraps its confirmation in.</summary>
internal sealed record DeleteWatchlistResponse
{
    [JsonPropertyName("data")]
    public DeleteWatchlistResponseData? Data { get; init; }
}

internal sealed record DeleteWatchlistResponseData
{
    [JsonPropertyName("deleted")]
    public string? Deleted { get; init; }
}

/// <summary>
/// The response to <c>POST /iserver/watchlist</c>.
/// </summary>
/// <remarks>
/// Deliberately omits <c>instruments</c>. IBKR documents that array as always empty on creation and
/// types it as a list of anything -- its own example shows a list of strings rather than the objects
/// the read endpoint returns. Since there is nothing in it worth reading, not declaring the member
/// leaves its shape irrelevant: unmapped members are skipped without being parsed.
/// </remarks>
internal sealed record CreatedWatchlistResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    [JsonPropertyName("readOnly")]
    public bool? IsReadOnly { get; init; }
}
