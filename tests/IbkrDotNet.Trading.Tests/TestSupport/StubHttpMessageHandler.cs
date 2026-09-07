using System.Net;
using System.Text;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// Records the requests it receives and replays a canned response, so endpoint clients can be
/// verified without a network.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<RecordedRequest, HttpResponseMessage>> _responders = new();
    private readonly List<RecordedRequest> _requests = [];
    private Func<RecordedRequest, HttpResponseMessage>? _fallback;

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    public RecordedRequest LastRequest =>
        _requests.Count > 0
            ? _requests[^1]
            : throw new InvalidOperationException("No request has been sent.");

    public StubHttpMessageHandler RespondWithJson(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        RespondWith(status, json);

    public StubHttpMessageHandler RespondWith(HttpStatusCode status, string body = "") =>
        RespondWith(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    public StubHttpMessageHandler RespondWith(HttpResponseMessage response)
    {
        _responders.Enqueue(_ => response);
        return this;
    }

    /// <summary>
    /// Responds by inspecting the request, for exchanges whose reply depends on what was sent -- a
    /// Diffie-Hellman handshake, for instance.
    /// </summary>
    public StubHttpMessageHandler RespondWith(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    /// <summary>
    /// Responds to every request the queue does not cover, for a stand-in server that must answer
    /// an unknown number of times.
    /// </summary>
    public StubHttpMessageHandler AlwaysRespondWith(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        _fallback = responder;
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

        if (_responders.Count > 0)
        {
            return _responders.Dequeue()(_requests[^1]);
        }

        return _fallback is not null
            ? _fallback(_requests[^1])
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
