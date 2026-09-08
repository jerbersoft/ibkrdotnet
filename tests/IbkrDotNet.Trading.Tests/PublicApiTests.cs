using IbkrDotNet.Tests.Shared;
using IbkrDotNet.Trading.Http;
using Xunit;

namespace IbkrDotNet.Trading.Tests;

public class PublicApiTests
{
    [Fact]
    public void The_public_surface_matches_its_approved_transcript() =>
        PublicApiApproval.Approve(typeof(IbkrApiClient).Assembly);
}
