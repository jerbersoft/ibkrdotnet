using IbkrDotNet.Trading.Http.RateLimiting;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

public class PathTemplateTests
{
    [Theory]
    [InlineData("/v1/api/tickle", "/v1/api/tickle", true)]
    [InlineData("/v1/api/tickle", "/v1/api/tickle/extra", false)]
    [InlineData("/v1/api/tickle", "/v1/api", false)]
    [InlineData("/v1/api/fyi/notifications/{id}", "/v1/api/fyi/notifications/abc123", true)]
    [InlineData("/v1/api/fyi/notifications/{id}", "/v1/api/fyi/notifications", false)]
    [InlineData("/v1/api/fyi/notifications/{id}", "/v1/api/fyi/notifications/a/b", false)]
    [InlineData("/v1/api/portfolio/accounts", "/V1/API/PORTFOLIO/ACCOUNTS", true)]
    public void Matches_paths_segment_by_segment(string template, string path, bool expected)
    {
        Assert.Equal(expected, PathTemplate.Parse(template).Matches(path));
    }

    [Fact]
    public void Does_not_confuse_the_literal_more_route_with_a_notification_id()
    {
        // '/fyi/notifications/more' and '/fyi/notifications/{notificationId}' both exist; the
        // literal template must be able to match its own route.
        Assert.True(PathTemplate.Parse("/v1/api/fyi/notifications/more").Matches("/v1/api/fyi/notifications/more"));
        Assert.False(PathTemplate.Parse("/v1/api/fyi/notifications/more").Matches("/v1/api/fyi/notifications/xyz"));
    }
}
