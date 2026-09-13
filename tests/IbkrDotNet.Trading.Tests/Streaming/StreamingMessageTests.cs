using System.Text.Json;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Models.Streaming;
using IbkrDotNet.Trading.Serialization;
using IbkrDotNet.Trading.Streaming;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Streaming;

public class StreamingMessageTests
{
    private static readonly Instant ReceivedAt = Instant.FromUtc(2024, 4, 8, 16, 41, 51);

    private static StreamingMessage Load(string fixture)
    {
        using var document = Fixture.ReadJson($"Responses/websocket/{fixture}");
        var root = document.RootElement.Clone();
        return new StreamingMessage(root.GetProperty("topic").GetString()!, root, ReceivedAt);
    }

    [Fact]
    public void Reads_the_documented_system_confirmation()
    {
        var message = Load("system-connection.json").Deserialize<StreamingSystemMessage>();

        Assert.Equal("system", message.Topic);
        Assert.Equal("success", message.Success);
        Assert.False(message.IsHeartbeat);
    }

    [Fact]
    public void Reads_a_heartbeat_as_an_instant()
    {
        // IBKR documents the heartbeat as "unix time in millisecond format" without an example.
        var body = JsonDocument.Parse("""{"topic":"system","hb":1712596911593}""").RootElement.Clone();
        var message = new StreamingMessage("system", body, ReceivedAt).Deserialize<StreamingSystemMessage>();

        Assert.True(message.IsHeartbeat);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1712596911593), message.Heartbeat);
    }

    [Fact]
    public void Reads_the_documented_authentication_status()
    {
        // The documented example carries a placeholder where the boolean goes; the fixture has 'true'.
        var message = Load("authentication-status.json").Deserialize<StreamingAuthenticationStatus>();

        Assert.Equal("sts", message.Topic);
        Assert.True(message.Args?.Authenticated);
    }

    [Fact]
    public void Reads_the_documented_bulletin_and_notification()
    {
        var bulletin = Load("bulletins.json").Deserialize<StreamingBulletin>();
        var notification = Load("notifications.json").Deserialize<StreamingNotification>();

        Assert.Equal("id", bulletin.Args?.Id);
        Assert.Equal("message", bulletin.Args?.Message);
        Assert.Equal("title", notification.Args?.Title);
        Assert.Equal("url", notification.Args?.Url);
    }

    [Fact]
    public void The_documented_market_data_message_reads_as_an_update()
    {
        // The streaming shape is the snapshot shape plus 'topic' and '6119'. The topic is its own
        // property; the second copy of the server identifier stays in Fields.
        var message = Load("market-data-response.json");
        var update = message.Deserialize<MarketDataUpdate>();

        Assert.Equal("smd+8314", message.Topic);
        Assert.Equal("smd+8314", update.Topic);
        Assert.Equal(8314, update.ConId.Value);
        Assert.Equal("8314", update.ConIdWithExchange);
        Assert.Equal("q2", update.ServerId);
        Assert.Equal(189.60m, update.LastPrice);
        Assert.Equal(189.56m, update.BidPrice);
        Assert.Equal(189.61m, update.AskPrice);
        Assert.Equal(200m, update.BidSize);
        Assert.Equal(500m, update.AskSize);
        Assert.Equal("100", update.GetString(MarketDataField.LastSize));
        Assert.Equal("RpB", update.MarketDataAvailability);
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1712596911593), update.UpdatedAt);
        Assert.Equal("q2", update.GetString("6119"));
        Assert.False(update.Fields.ContainsKey("topic"));
        Assert.Null(update.Volume);
    }

    [Fact]
    public void The_documented_market_data_message_still_reads_as_a_snapshot()
    {
        // The snapshot type is what a caller going through the transport directly may reach for.
        var snapshot = Load("market-data-response.json").Deserialize<MarketDataSnapshot>();

        Assert.Equal(8314, snapshot.ConId.Value);
        Assert.Equal(189.60m, snapshot.LastPrice);
        Assert.Equal("smd+8314", snapshot.GetString("topic"));
    }

    [Fact]
    public void Names_the_topic_when_a_message_cannot_be_read_as_the_requested_type()
    {
        var body = JsonDocument.Parse("""{"topic":"sts","args":"not an object"}""").RootElement.Clone();
        var message = new StreamingMessage("sts", body, ReceivedAt);

        var ex = Assert.Throws<IbkrSerializationException>(() => message.Deserialize<StreamingAuthenticationStatus>());

        Assert.Contains("'sts'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prints_as_the_message_ibkr_sent()
    {
        var message = Load("system-connection.json");

        Assert.Contains("\"success\"", message.ToString(), StringComparison.Ordinal);
    }
}
