using IbkrDotNet.Trading.Http;

namespace IbkrDotNet.Trading.Streaming;

/// <summary>
/// Thrown when the WebSocket could not be opened, was lost, or refused a frame.
/// </summary>
/// <remarks>
/// An <see cref="IbkrApiException"/> so that a caller handling IBKR failures in one place catches
/// these too. Authentication failures on the upgrade are reported as
/// <see cref="IbkrAuthenticationException"/>, the same as over HTTP.
/// </remarks>
public sealed class IbkrStreamingException : IbkrApiException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    public IbkrStreamingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public IbkrStreamingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
