using IbkrDotNet.Trading.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// Wires an <see cref="IbkrApiClient"/> to a <see cref="StubHttpMessageHandler"/> so endpoint
/// clients can be exercised against IBKR's own documented payloads.
/// </summary>
public sealed class ClientHarness : IDisposable
{
    private readonly HttpClient _httpClient;

    public ClientHarness()
    {
        Stub = new StubHttpMessageHandler();
        _httpClient = new HttpClient(Stub) { BaseAddress = new Uri("https://localhost:5000") };
        ApiClient = new IbkrApiClient(_httpClient, NullLogger<IbkrApiClient>.Instance);
    }

    public StubHttpMessageHandler Stub { get; }

    public IIbkrApiClient ApiClient { get; }

    public StubHttpMessageHandler.RecordedRequest LastRequest => Stub.LastRequest;

    /// <summary>Replays a fixture extracted from IBKR's endpoint reference.</summary>
    public ClientHarness RespondWithFixture(string relativePath)
    {
        Stub.RespondWithJson(Fixture.ReadText($"Responses/{relativePath}"));
        return this;
    }

    public ClientHarness RespondWithJson(string json)
    {
        Stub.RespondWithJson(json);
        return this;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        Stub.Dispose();
    }
}
