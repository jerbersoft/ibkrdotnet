using System.Text.Json;
using IbkrDotNet.Trading.Models.Contracts;
using IbkrDotNet.Trading.Serialization;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Serialization;

public class SingleOrArrayConverterTests
{
    // IBKR documents displayRule as an array on /trsrv/secdef; a live gateway sends a bare object
    // for the same field on the same endpoint.
    private const string DisplayRule =
        """{"magnification":0,"displayRuleStep":[{"decimalDigits":2,"lowerEdge":0.0,"wholeDigits":4}]}""";

    [Fact]
    public void Reads_the_documented_array_shape()
    {
        var definition = JsonSerializer.Deserialize<InstrumentDefinition>(
            $$"""{"conid":265598,"displayRule":[{{DisplayRule}}]}""",
            IbkrJson.Options);

        var rule = Assert.Single(definition!.DisplayRules);
        Assert.Equal(0, rule.Magnification);
        Assert.Equal(2, Assert.Single(rule.Steps).DecimalDigits);
    }

    [Fact]
    public void Reads_the_single_object_shape_a_live_gateway_sends()
    {
        var definition = JsonSerializer.Deserialize<InstrumentDefinition>(
            $$"""{"conid":265598,"displayRule":{{DisplayRule}}}""",
            IbkrJson.Options);

        var rule = Assert.Single(definition!.DisplayRules);
        Assert.Equal(0, rule.Magnification);
        Assert.Equal(2, Assert.Single(rule.Steps).DecimalDigits);
    }

    [Fact]
    public void Reads_a_missing_value_as_an_empty_list()
    {
        var definition = JsonSerializer.Deserialize<InstrumentDefinition>(
            """{"conid":265598}""",
            IbkrJson.Options);

        Assert.Empty(definition!.DisplayRules);
    }

    [Fact]
    public void Writes_a_single_value_back_as_an_array()
    {
        var definition = JsonSerializer.Deserialize<InstrumentDefinition>(
            $$"""{"conid":265598,"displayRule":{{DisplayRule}}}""",
            IbkrJson.Options);

        var json = JsonSerializer.Serialize(definition, IbkrJson.Options);

        Assert.Contains("""display""", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("displayRule").ValueKind);
    }
}
