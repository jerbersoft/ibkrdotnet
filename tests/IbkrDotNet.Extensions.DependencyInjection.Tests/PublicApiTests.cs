using IbkrDotNet.Tests.Shared;
using Xunit;

namespace IbkrDotNet.Extensions.DependencyInjection.Tests;

public class PublicApiTests
{
    [Fact]
    public void The_public_surface_matches_its_approved_transcript() =>
        PublicApiApproval.Approve(typeof(IbkrTradingServiceCollectionExtensions).Assembly);
}
