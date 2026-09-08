using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;

namespace IbkrDotNet.Trading.Models.Scanner;

/// <summary>The body of <c>POST /iserver/scanner/run</c>.</summary>
/// <param name="Instrument">The instrument type, from <see cref="ScannerInstrument.Type"/>.</param>
/// <param name="Type">The scan type, from <see cref="ScannerType.Code"/>.</param>
/// <param name="Location">The location, from <see cref="ScannerLocation.Type"/>.</param>
/// <param name="Filter">The filters to apply. IBKR names this single key for a list.</param>
internal sealed record ScannerRequest(
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("location")] string? Location,
    [property: JsonPropertyName("filter")] IReadOnlyList<ScannerFilter> Filter);

/// <summary>
/// One filter applied to a scanner request.
/// </summary>
/// <param name="Code">The filter's code, from <see cref="ScannerFilterDefinition.Code"/>.</param>
/// <param name="Value">
/// The value to filter on. IBKR accepts a string, an integer, a floating-point number or a boolean
/// here depending on the filter, and does not say which a given code expects, so the type is left
/// open rather than guessed at.
/// </param>
public sealed record ScannerFilter(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] object? Value);

/// <summary>The contracts a market scanner matched, from <c>POST /iserver/scanner/run</c>.</summary>
public sealed record ScannerResults
{
    /// <summary>The matching contracts, in the scan's own sort order.</summary>
    [JsonPropertyName("contracts")]
    public IReadOnlyList<ScannerContract> Contracts { get; init; } = [];

    /// <summary>
    /// The heading for the value the scan sorted on, for example <c>Chg%</c> or <c>Trades</c>.
    /// </summary>
    /// <remarks>
    /// IBKR marks this internal use, but it is the label for <see cref="ScannerContract.ScanData"/>
    /// and there is nowhere else to get it.
    /// </remarks>
    [JsonPropertyName("scan_data_column_name")]
    public string? ScanDataColumnName { get; init; }
}

/// <summary>One contract matched by a market scanner.</summary>
public sealed record ScannerContract
{
    /// <summary>The row's position in the scan's sort order, as a string starting at <c>"0"</c>.</summary>
    [JsonPropertyName("server_id")]
    public string? ServerId { get; init; }

    /// <summary>The contract identifier.</summary>
    [JsonPropertyName("con_id")]
    public ConId ConId { get; init; }

    /// <summary>The contract identifier and routing destination, as <c>123456@EXCHANGE</c>.</summary>
    /// <remarks>A scan sends the conid alone here, without the <c>@EXCHANGE</c> half.</remarks>
    [JsonPropertyName("conidex")]
    public string? ConIdWithExchange { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The company's long name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>
    /// The contract's description. For derivatives this is the local symbol of the contract.
    /// </summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription { get; init; }

    /// <summary>The primary listing exchange.</summary>
    [JsonPropertyName("listing_exchange")]
    public string? ListingExchange { get; init; }

    /// <summary>The asset class.</summary>
    [JsonPropertyName("sec_type")]
    public string? SecurityType { get; init; }

    /// <summary>
    /// The value the scan sorted on, formatted for display, for example <c>221.521K</c>.
    /// </summary>
    /// <remarks>
    /// Expect it to be absent. IBKR's published example carries it on every row, but a live gateway
    /// omitted it from all fifty rows of a Top % Gainers scan, both before the open and during
    /// regular trading hours -- it returned the contracts and the column heading and nothing else.
    /// Treat a scan as a way to select contracts, not as a source of the metric it selected them by.
    /// </remarks>
    [JsonPropertyName("scan_data")]
    public string? ScanData { get; init; }

    /// <summary>
    /// The heading for <see cref="ScanData"/>, sent on the first row only.
    /// </summary>
    /// <remarks>
    /// The same value as <see cref="ScannerResults.ScanDataColumnName"/>, which is where to read it
    /// from; this is here because IBKR sends it.
    /// </remarks>
    [JsonPropertyName("column_name")]
    public string? ColumnName { get; init; }

    /// <summary>The chart periods available for the contract. IBKR marks this internal use.</summary>
    [JsonPropertyName("available_chart_periods")]
    public string? AvailableChartPeriods { get; init; }
}
