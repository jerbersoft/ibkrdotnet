using System.Net;
using System.Text;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// Records the requests it receives and replays a canned response, so endpoint clients can be
/// verified without a network.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    public RecordedRequest LastRequest =>
        _requests.Count > 0
            ? _requests[^1]
            : throw new InvalidOperationException("No request has been sent.");

    public StubHttpMessageHandler RespondWithJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        return this;
    }

    public StubHttpMessageHandler RespondWith(HttpStatusCode status, string body = "")
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        return this;
    }

    public StubHttpMessageHandler RespondWith(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase)));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
    }

    public sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Body,
        IReadOnlyDictionary<string, string> Headers)
    {
        public string Path => Uri.AbsolutePath;

        public string Query => Uri.Query;
    }
}
