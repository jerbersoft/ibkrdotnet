namespace IbkrDotNet.Trading.Streaming;

/// <summary>The state of the WebSocket transport.</summary>
public enum StreamingConnectionState
{
    /// <summary>No socket is open. The initial state, and the state after <c>CloseAsync</c>.</summary>
    Disconnected,

    /// <summary>The brokerage session is being confirmed and the socket opened.</summary>
    Connecting,

    /// <summary>The socket is open and messages are being read.</summary>
    Connected,

    /// <summary>The socket dropped and the transport is waiting to reopen it.</summary>
    Reconnecting,
}
