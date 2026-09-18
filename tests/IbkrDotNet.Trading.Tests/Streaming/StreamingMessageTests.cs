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

    /// <summary>Loads one frame of a fixture that recorded several.</summary>
    private static StreamingMessage LoadFrame(string fixture, int index)
    {
        using var document = Fixture.ReadJson($"Responses/websocket/{fixture}");
        var frame = document.RootElement[index].Clone();
        return new StreamingMessage(frame.GetProperty("topic").GetString()!, frame, ReceivedAt);
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

        // IBKR documents 'args' as a bare object and a gateway sends an array; the examples still read.
        var args = Assert.Single(bulletin.Args);
        Assert.Equal("id", args.Id);
        Assert.Equal("message", args.Message);

        var notice = Assert.IsType<StreamingNotificationArgs.Notice>(Assert.Single(notification.Args));
        Assert.Equal("title", notice.Title);
        Assert.Equal("url", notice.Url);
    }

    [Fact]
    public void Reads_a_recorded_bulletin_whose_args_is_an_array()
    {
        var bulletin = Load("bulletins.live.json").Deserialize<StreamingBulletin>();

        // The identifier is documented as a string and sent as a number, and 'exchanges' is sent and
        // documented nowhere; neither stops the bulletin being read.
        var args = Assert.Single(bulletin.Args);
        Assert.Equal("1789618148", args.Id);
        Assert.StartsWith("[BBSMSG Bulletin]", args.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_more_than_one_bulletin_from_one_message()
    {
        // Nothing says a gateway sends bulletins one to a frame, and the array is what would carry
        // two.
        var body = JsonDocument.Parse(
            """{"topic":"blt","args":[{"id":1,"message":"first"},{"id":2,"message":"second"}]}""")
            .RootElement.Clone();
        var bulletin = new StreamingMessage("blt", body, ReceivedAt).Deserialize<StreamingBulletin>();

        Assert.Equal(["first", "second"], bulletin.Args.Select(args => args.Message));
    }

    [Fact]
    public void Reads_a_recorded_notification_whose_args_is_an_array()
    {
        var notification = LoadFrame("notifications.live.json", 0).Deserialize<StreamingNotification>();

        var notice = Assert.IsType<StreamingNotificationArgs.Notice>(Assert.Single(notification.Args));
        Assert.Equal("118", notice.Id);
        Assert.StartsWith("Order SELL 1 MSFT", notice.Text, StringComparison.Ordinal);
        Assert.Null(notice.Title);
        Assert.Null(notice.Url);
    }

    [Fact]
    public void Reads_a_recorded_notification_that_is_a_prompt()
    {
        var notification = LoadFrame("notifications.live.json", 1).Deserialize<StreamingNotification>();

        var prompt = Assert.IsType<StreamingNotificationArgs.Prompt>(Assert.Single(notification.Args));
        Assert.Equal(1883733600, prompt.OrderId?.Value);
        Assert.Equal("29042", prompt.RequestId);
        Assert.Equal("p12", prompt.MessageId);
        Assert.Equal("M", prompt.Type);
        Assert.True(prompt.IsPrompt);
        Assert.Empty(prompt.Dismissable);
        Assert.Equal(["Use on this order", "Always use", "Do not use"], prompt.Options);
        Assert.EndsWith("Use the Price Management Algo?", prompt.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Tells_a_notice_from_a_prompt_within_one_message()
    {
        // Nothing says IBKR sends the two shapes in separate frames, and the array is what would
        // carry them together.
        var body = JsonDocument.Parse(
            """
            {"topic":"ntf","args":[{"id":"118","text":"notice"},{"orderId":7,"reqId":"1","text":"question"}]}
            """).RootElement.Clone();
        var notification = new StreamingMessage("ntf", body, ReceivedAt).Deserialize<StreamingNotification>();

        Assert.Collection(
            notification.Args,
            first => Assert.Equal("118", Assert.IsType<StreamingNotificationArgs.Notice>(first).Id),
            second => Assert.Equal(7, Assert.IsType<StreamingNotificationArgs.Prompt>(second).OrderId?.Value));

        // Both shapes answer the one question a caller has before anything else.
        Assert.Equal(["notice", "question"], notification.Args.Select(arg => arg.Text));
    }

    [Fact]
    public void Writes_a_prompt_back_as_the_shape_it_was_read_as()
    {
        var notification = LoadFrame("notifications.live.json", 1).Deserialize<StreamingNotification>();

        var json = JsonSerializer.Serialize(notification, IbkrJson.Options);

        using var written = JsonDocument.Parse(json);
        var prompt = written.RootElement.GetProperty("args")[0];
        Assert.Equal(1883733600, prompt.GetProperty("orderId").GetInt64());
        Assert.Equal("p12", prompt.GetProperty("messageId").GetString());
        Assert.False(prompt.TryGetProperty("url", out _));
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
