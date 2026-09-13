using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.Options;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Runs <see cref="IbkrStreamingOptions.Validate"/> with the rest of the options validation, so a
/// streaming setting that is out of range fails at startup rather than when the socket is first opened.
/// </summary>
internal sealed class IbkrStreamingOptionsValidator : IValidateOptions<IbkrTradingOptions>
{
    public ValidateOptionsResult Validate(string? name, IbkrTradingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.Streaming.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
