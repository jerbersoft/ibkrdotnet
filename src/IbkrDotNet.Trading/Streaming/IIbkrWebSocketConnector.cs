using System.Net.WebSockets;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// Everything the transport needs to open the socket, resolved before the upgrade request is sent.
/// </summary>
/// <param name="Address">The socket address, query string included.</param>
/// <param name="Headers">The headers the upgrade request carries: the session cookie, the user agent and the origin.</param>
/// <param name="Configure">The caller's hook over the client options, run after the headers are set.</param>
public sealed record StreamingConnectRequest(
    Uri Address,
    IReadOnlyDictionary<string, string> Headers,
    Action<ClientWebSocketOptions>? Configure);

/// <summary>
/// Opens a WebSocket for the transport.
/// </summary>
/// <remarks>
/// <see cref="ClientWebSocket"/> is sealed and talks to the network, so the transport takes the
/// connection through this seam instead. <see cref="ClientWebSocketConnector"/> is the real one;
/// tests substitute a fake that hands back a scripted <see cref="WebSocket"/>.
/// </remarks>
public interface IIbkrWebSocketConnector
{
    /// <summary>Opens a socket, completing the upgrade handshake.</summary>
    /// <param name="request">Where to connect and what to send.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>An open socket.</returns>
    Task<WebSocket> ConnectAsync(StreamingConnectRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The default <see cref="IIbkrWebSocketConnector"/>, over <see cref="ClientWebSocket"/>.
/// </summary>
public sealed class ClientWebSocketConnector : IIbkrWebSocketConnector
{
    /// <summary>A shared instance; the type holds no state.</summary>
    public static ClientWebSocketConnector Instance { get; } = new();

    /// <inheritdoc />
    public async Task<WebSocket> ConnectAsync(StreamingConnectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var socket = new ClientWebSocket();
        try
        {
            foreach (var (name, value) in request.Headers)
            {
                socket.Options.SetRequestHeader(name, value);
            }

            request.Configure?.Invoke(socket.Options);

            await socket.ConnectAsync(request.Address, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
