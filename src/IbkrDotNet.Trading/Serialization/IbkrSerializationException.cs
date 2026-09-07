namespace IbkrDotNet.Trading.Serialization;

/// <summary>
/// Thrown when a value on the wire cannot be interpreted in the format the Interactive Brokers
/// documentation specifies for it.
/// </summary>
/// <remarks>
/// The client fails loudly rather than silently substituting <see langword="null"/> for a value it
/// cannot read. In a trading context a missing timestamp or price that quietly becomes null is far
/// more dangerous than an exception naming the exact value that could not be parsed.
/// </remarks>
public sealed class IbkrSerializationException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    public IbkrSerializationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public IbkrSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The raw wire value that could not be interpreted, when available.</summary>
    public string? RawValue { get; init; }

    /// <summary>The expected wire format, when available.</summary>
    public string? ExpectedFormat { get; init; }

    internal static IbkrSerializationException ForValue(
        string? rawValue,
        string expectedFormat,
        Type targetType) =>
        new($"Could not read '{rawValue}' as {targetType.Name}; expected the format {expectedFormat}.")
        {
            RawValue = rawValue,
            ExpectedFormat = expectedFormat,
        };
}
