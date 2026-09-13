using System.Net.WebSockets;
using IbkrDotNet.Trading.Streaming;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// Hands the transport a <see cref="FakeWebSocket"/> per connect, recording what it was asked for.
/// </summary>
public sealed class FakeWebSocketConnector : IIbkrWebSocketConnector
{
    private readonly Lock _sync = new();
    private readonly List<StreamingConnectRequest> _requests = [];
    private readonly List<FakeWebSocket> _sockets = [];
    private readonly Queue<Exception> _failures = new();

    public IReadOnlyList<StreamingConnectRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return [.. _requests];
            }
        }
    }

    public IReadOnlyList<FakeWebSocket> Sockets
    {
        get
        {
            lock (_sync)
            {
                return [.. _sockets];
            }
        }
    }

    public StreamingConnectRequest LastRequest => Requests[^1];

    public FakeWebSocket Socket => Sockets[^1];

    /// <summary>Makes the next connect attempt fail, the way an unreachable gateway would.</summary>
    public void FailNextConnect(Exception failure)
    {
        lock (_sync)
        {
            _failures.Enqueue(failure);
        }
    }

    /// <summary>Waits until the connector has handed out its <paramref name="index"/>th socket.</summary>
    public async Task<FakeWebSocket> WaitForSocketAsync(int index, CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_sync)
            {
                if (_sockets.Count > index)
                {
                    return _sockets[index];
                }
            }

            await Task.Delay(10, cancellationToken);
        }
    }

    public Task<WebSocket> ConnectAsync(StreamingConnectRequest request, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _requests.Add(request);
            if (_failures.TryDequeue(out var failure))
            {
                return Task.FromException<WebSocket>(failure);
            }

            var socket = new FakeWebSocket();
            _sockets.Add(socket);
            return Task.FromResult<WebSocket>(socket);
        }
    }
}
