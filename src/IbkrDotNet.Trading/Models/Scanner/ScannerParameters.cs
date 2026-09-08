using System.Text.Json.Serialization;

namespace IbkrDotNet.Trading.Models.Scanner;

/// <summary>
/// Everything a market scanner request can be built from, from
/// <c>GET /iserver/scanner/params</c>.
/// </summary>
/// <remarks>
/// A live gateway returns roughly 200 KB here: around 580 scan types, 18 instrument types, 615
/// filters and a two-level location tree. It is reference data that changes rarely, and IBKR permits
/// one request per fifteen minutes, so hold the result rather than fetching it per scan.
/// </remarks>
public sealed record ScannerParameters
{
    /// <summary>The scan types, supplying the <c>type</c> of a scanner request.</summary>
    [JsonPropertyName("scan_type_list")]
    public IReadOnlyList<ScannerType> ScanTypes { get; init; } = [];

    /// <summary>The instrument types, supplying the <c>instrument</c> of a scanner request.</summary>
    [JsonPropertyName("instrument_list")]
    public IReadOnlyList<ScannerInstrument> Instruments { get; init; } = [];

    /// <summary>The filters that can be applied to a scanner request.</summary>
    [JsonPropertyName("filter_list")]
    public IReadOnlyList<ScannerFilterDefinition> Filters { get; init; } = [];

    /// <summary>The locations, supplying the <c>location</c> of a scanner request.</summary>
    [JsonPropertyName("location_tree")]
    public IReadOnlyList<ScannerLocation> Locations { get; init; } = [];
}

/// <summary>One scan type, such as "Top % Gainers".</summary>
public sealed record ScannerType
{
    /// <summary>The name as displayed, for example <c>Top % Gainers</c>.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>The value to send as a scanner request's <c>type</c>, for example <c>TOP_PERC_GAIN</c>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>The instrument types this scan can be run against.</summary>
    [JsonPropertyName("instruments")]
    public IReadOnlyList<string> Instruments { get; init; } = [];
}

/// <summary>One instrument type a scan can be run against, such as "US Stocks".</summary>
public sealed record ScannerInstrument
{
    /// <summary>The name as displayed, for example <c>US Stocks</c>.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>The value to send as a scanner request's <c>instrument</c>, for example <c>STK</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The codes of the filters available for this instrument type.</summary>
    [JsonPropertyName("filters")]
    public IReadOnlyList<string> Filters { get; init; } = [];
}

/// <summary>One filter a scanner request may carry.</summary>
public sealed record ScannerFilterDefinition
{
    /// <summary>The group the filter belongs to.</summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>The name as displayed, for example <c>After-Hours Change Above</c>.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>The value to send as a <see cref="ScannerFilter.Code"/>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>
    /// The kind of value the filter takes. A live gateway sends only <c>non-range</c> and
    /// <c>combo</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The choices a <c>combo</c> filter offers.</summary>
    [JsonPropertyName("combo_values")]
    public IReadOnlyList<ScannerComboValue> ComboValues { get; init; } = [];
}

/// <summary>
/// One choice offered by a <c>combo</c> filter.
/// </summary>
/// <remarks>
/// IBKR sends these without a value or a label, so a combo filter's permissible values cannot be
/// discovered from this endpoint: the "Halted" filter, for instance, returns three entries whose
/// only content is which of them is the default. Determining what to send for a combo filter means
/// reading it out of Trader Workstation.
/// </remarks>
public sealed record ScannerComboValue
{
    /// <summary>Whether this is the choice applied when the filter is not set.</summary>
    [JsonPropertyName("default")]
    public bool? IsDefault { get; init; }
}

/// <summary>
/// One location a scan can be run over, such as "US Stocks" or "CME".
/// </summary>
/// <remarks>
/// The tree is two levels deep in practice, and a leaf omits <c>locations</c> rather than sending an
/// empty array.
/// </remarks>
public sealed record ScannerLocation
{
    /// <summary>The name as displayed, for example <c>Listed/NASDAQ</c>.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>The value to send as a scanner request's <c>location</c>, for example <c>STK.US.MAJOR</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The locations nested under this one.</summary>
    [JsonPropertyName("locations")]
    public IReadOnlyList<ScannerLocation> Locations { get; init; } = [];
}
