using IbkrDotNet.Trading.Streaming;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Streaming;

public class StreamingFrameTests
{
    private static readonly string[] IbmFields = ["31", "84", "85", "86", "88", "7059"];
    private static readonly string[] SubmittedOnly = ["Submitted"];

    [Fact]
    public void Renders_topic_target_and_parameters_joined_with_plus()
    {
        var frame = StreamingFrame.Encode("smd", "8314", new { fields = IbmFields });

        // IBKR's documented IBM example, field tags as JSON strings.
        Assert.Equal("""smd+8314+{"fields":["31","84","85","86","88","7059"]}""", frame);
    }

    [Fact]
    public void Omits_the_target_when_the_topic_takes_none()
    {
        Assert.Equal("""sor+{"filters":["Submitted"]}""", StreamingFrame.Encode("sor", null, new { filters = SubmittedOnly }));
    }

    [Fact]
    public void Renders_a_bare_topic_when_there_is_nothing_else()
    {
        Assert.Equal("tic", StreamingFrame.Encode("tic"));
    }

    [Fact]
    public void Renders_empty_parameters_as_an_empty_object()
    {
        Assert.Equal("umd+8314+{}", StreamingFrame.Encode("umd", "8314", StreamingFrame.EmptyParameters));
        Assert.Equal("spl+{}", StreamingFrame.Encode("spl", null, StreamingFrame.EmptyParameters));
    }

    [Fact]
    public void Leaves_a_target_with_its_own_separators_alone()
    {
        // The price ladder target is account, conid and exchange joined with the same separator.
        Assert.Equal("sbd+DU1234567+265598+SMART", StreamingFrame.Encode("sbd", "DU1234567+265598+SMART"));
    }
}

public class StreamingSubscriptionRequestTests
{
    private static readonly string[] LastPriceOnly = ["31"];

    [Fact]
    public void A_solicited_request_derives_its_unsubscribe_topic_and_routing_key()
    {
        var request = StreamingSubscriptionRequest.Solicited("smd", "8314", new { fields = LastPriceOnly });

        Assert.True(request.IsSolicited);
        Assert.Equal("smd+8314", request.RoutingKey);
        Assert.Equal("""smd+8314+{"fields":["31"]}""", request.SubscribeFrame);
        Assert.Equal("umd+8314+{}", request.UnsubscribeFrame);
    }

    [Fact]
    public void A_solicited_request_without_parameters_still_sends_the_empty_object()
    {
        var request = StreamingSubscriptionRequest.Solicited("spl");

        Assert.Equal("spl+{}", request.SubscribeFrame);
        Assert.Equal("upl+{}", request.UnsubscribeFrame);
        Assert.Equal("spl", request.RoutingKey);
    }

    [Fact]
    public void An_unsolicited_request_sends_nothing()
    {
        var request = StreamingSubscriptionRequest.Unsolicited("sts");

        Assert.False(request.IsSolicited);
        Assert.Null(request.SubscribeFrame);
        Assert.Null(request.UnsubscribeFrame);
        Assert.Equal("sts", request.RoutingKey);
    }

    [Theory]
    [InlineData("umd")]
    [InlineData("tic")]
    [InlineData("system")]
    public void Refuses_to_treat_a_non_subscribe_topic_as_solicited(string topic)
    {
        var ex = Assert.Throws<ArgumentException>(() => StreamingSubscriptionRequest.Solicited(topic));

        Assert.Contains("not a solicited topic", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Matches_an_inbound_topic_exactly_by_default()
    {
        var request = StreamingSubscriptionRequest.Solicited("smd", "8314");

        Assert.True(request.Matches("smd+8314"));
        Assert.False(request.Matches("smd+83141"));
        Assert.False(request.Matches("smd+8314+extra"));
        Assert.False(request.Matches("smd"));
    }

    [Fact]
    public void Matches_a_prefix_when_the_response_appends_something_the_request_could_not_know()
    {
        // Historical data answers on smh+{serverId}, and the server identifier is only known afterwards.
        var request = StreamingSubscriptionRequest.Solicited("smh", "8314") with
        {
            ResponseTopic = "smh",
            MatchResponseTopicPrefix = true,
        };

        Assert.True(request.Matches("smh"));
        Assert.True(request.Matches("smh+q2"));
        Assert.False(request.Matches("smd+q2"));
        Assert.False(request.Matches("smhq2"));
    }
}
