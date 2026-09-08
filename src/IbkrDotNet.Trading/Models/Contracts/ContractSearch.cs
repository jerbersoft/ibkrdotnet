using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Models.Contracts;

/// <summary>An instrument matched by <c>GET /iserver/secdef/search</c>.</summary>
public sealed record ContractSearchResult
{
    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The company name with its exchange, as displayed.</summary>
    [JsonPropertyName("companyHeader")]
    public string? CompanyHeader { get; init; }

    /// <summary>The company name.</summary>
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; init; }

    /// <summary>A description of the instrument, typically its exchange.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Whether the instrument is restricted.</summary>
    [JsonPropertyName("restricted")]
    public string? Restricted { get; init; }

    /// <summary>The instrument's asset class.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>
    /// Option expiries available on the underlying, as <c>yyyyMMdd</c> dates joined by semicolons.
    /// </summary>
    /// <remarks>Use <see cref="OptionExpiries"/> to read them as dates.</remarks>
    [JsonPropertyName("opt")]
    public string? Options { get; init; }

    /// <summary>Futures option expiries, as <c>yyyyMMdd</c> dates joined by semicolons.</summary>
    [JsonPropertyName("fop")]
    public string? FuturesOptions { get; init; }

    /// <summary>Warrant expiries, as <c>yyyyMMdd</c> dates joined by semicolons.</summary>
    [JsonPropertyName("war")]
    public string? Warrants { get; init; }

    /// <summary>The instrument's sections, where IBKR returns them.</summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<ContractSection> Sections { get; init; } = [];

    /// <summary>The option expiries available on the underlying.</summary>
    public IReadOnlyList<LocalDate> OptionExpiries => ParseExpiries(Options);

    /// <summary>The futures option expiries available on the underlying.</summary>
    public IReadOnlyList<LocalDate> FuturesOptionExpiries => ParseExpiries(FuturesOptions);

    /// <summary>The warrant expiries available on the underlying.</summary>
    public IReadOnlyList<LocalDate> WarrantExpiries => ParseExpiries(Warrants);

    private static List<LocalDate> ParseExpiries(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var results = new List<LocalDate>();
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parsed = Time.IbkrTimePatterns.Date.Parse(part);
            if (parsed.Success)
            {
                results.Add(parsed.Value);
            }
        }

        return results;
    }
}

/// <summary>A tradable section of an instrument returned by a contract search.</summary>
public sealed record ContractSection
{
    /// <summary>The section's asset class.</summary>
    [JsonPropertyName("secType")]
    public string? SecurityType { get; init; }

    /// <summary>The section's months, joined by semicolons.</summary>
    [JsonPropertyName("months")]
    public string? Months { get; init; }

    /// <summary>The exchanges the section trades on.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>The section's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The section's contract identifier, where it has its own.</summary>
    [JsonPropertyName("conid")]
    public ConId? ConId { get; init; }

    /// <summary>The exchange the section is listed on.</summary>
    [JsonPropertyName("legSecType")]
    public string? LegSecurityType { get; init; }
}

/// <summary>The response from <c>GET /iserver/secdef/strikes</c>.</summary>
public sealed record OptionStrikes
{
    /// <summary>The available call strikes.</summary>
    [JsonPropertyName("call")]
    public IReadOnlyList<decimal> Call { get; init; } = [];

    /// <summary>The available put strikes.</summary>
    [JsonPropertyName("put")]
    public IReadOnlyList<decimal> Put { get; init; } = [];
}

/// <summary>The response from <c>GET /iserver/exchangerate</c>.</summary>
public sealed record ExchangeRate
{
    /// <summary>The rate from the source currency to the target currency.</summary>
    [JsonPropertyName("rate")]
    public decimal Rate { get; init; }
}

/// <summary>A tradable currency pair, from <c>GET /iserver/currency/pairs</c>.</summary>
public sealed record CurrencyPair
{
    /// <summary>The pair's symbol, for example <c>USD.SGD</c>.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The pair's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The counter currency.</summary>
    [JsonPropertyName("ccyPair")]
    public string? CounterCurrency { get; init; }
}

/// <summary>An instrument listed on an exchange, from <c>GET /trsrv/all-conids</c>.</summary>
public sealed record ExchangeListing
{
    /// <summary>The ticker symbol.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    /// <summary>The instrument's contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The exchange.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }
}

/// <summary>A futures contract, from <c>GET /trsrv/futures</c>.</summary>
public sealed record FutureContract
{
    /// <summary>The contract identifier.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The underlying's symbol.</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    /// <summary>The contract's expiration date.</summary>
    [JsonPropertyName("expirationDate")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? ExpirationDate { get; init; }

    /// <summary>The last day the contract trades.</summary>
    [JsonPropertyName("ltd")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? LastTradingDay { get; init; }

    /// <summary>The cut-off date for long positions.</summary>
    [JsonPropertyName("longFuturesCutOff")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? LongFuturesCutOff { get; init; }

    /// <summary>The cut-off date for short positions.</summary>
    [JsonPropertyName("shortFuturesCutOff")]
    [JsonConverter(typeof(IbkrLocalDateConverter))]
    public LocalDate? ShortFuturesCutOff { get; init; }

    /// <summary>The underlying instrument's contract identifier.</summary>
    [JsonPropertyName("underlyingConid")]
    public ConId? UnderlyingConId { get; init; }
}

/// <summary>A stock and the exchanges it trades on, from <c>GET /trsrv/stocks</c>.</summary>
public sealed record StockListing
{
    /// <summary>The company name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The company name in Chinese.</summary>
    [JsonPropertyName("chineseName")]
    public string? ChineseName { get; init; }

    /// <summary>The asset class.</summary>
    [JsonPropertyName("assetClass")]
    public string? AssetClass { get; init; }

    /// <summary>The listings across exchanges.</summary>
    [JsonPropertyName("contracts")]
    public IReadOnlyList<StockListingContract> Contracts { get; init; } = [];
}

/// <summary>One exchange listing of a stock.</summary>
public sealed record StockListingContract
{
    /// <summary>The contract identifier for this listing.</summary>
    [JsonPropertyName("conid")]
    public ConId ConId { get; init; }

    /// <summary>The exchange.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>Whether the listing is in the United States.</summary>
    [JsonPropertyName("isUS")]
    public bool? IsUnitedStates { get; init; }
}
