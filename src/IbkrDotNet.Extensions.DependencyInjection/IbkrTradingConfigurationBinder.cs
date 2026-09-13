using System.Globalization;
using IbkrDotNet.Trading.Configuration;
using Microsoft.Extensions.Configuration;
using NodaTime;
using NodaTime.Text;

namespace IbkrDotNet.Extensions.DependencyInjection;

/// <summary>
/// Binds <see cref="IbkrTradingOptions"/> from configuration.
/// </summary>
/// <remarks>
/// Written by hand rather than delegating to the reflection binder because the options use NodaTime
/// <see cref="Duration"/> values, which the binder cannot construct.
/// </remarks>
internal static class IbkrTradingConfigurationBinder
{
    public static void Bind(IbkrTradingOptions options, IConfiguration configuration)
    {
        if (Enum.TryParse<IbkrEnvironment>(configuration["Environment"], ignoreCase: true, out var environment))
        {
            options.Environment = environment;
        }

        if (configuration["BaseAddress"] is { Length: > 0 } baseAddress)
        {
            options.BaseAddress = Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri)
                ? uri
                : throw new InvalidOperationException(
                    $"'{baseAddress}' is not a valid absolute URI for {nameof(IbkrTradingOptions.BaseAddress)}.");
        }

        if (configuration["UserAgent"] is { Length: > 0 } userAgent)
        {
            options.UserAgent = userAgent;
        }

        if (TryParseDuration(configuration["Timeout"], out var timeout))
        {
            options.Timeout = timeout;
        }

        var rateLimiting = configuration.GetSection("RateLimiting");
        if (rateLimiting.Exists())
        {
            if (bool.TryParse(rateLimiting["Enabled"], out var enabled))
            {
                options.RateLimiting.Enabled = enabled;
            }

            if (bool.TryParse(rateLimiting["EnforceGlobalLimit"], out var enforceGlobal))
            {
                options.RateLimiting.EnforceGlobalLimit = enforceGlobal;
            }

            if (TryParseDuration(rateLimiting["MaxWait"], out var maxWait))
            {
                options.RateLimiting.MaxWait = maxWait;
            }

            if (TryParseDuration(rateLimiting["DefaultRetryAfter"], out var defaultRetryAfter))
            {
                options.RateLimiting.DefaultRetryAfter = defaultRetryAfter;
            }
        }

        var streaming = configuration.GetSection("Streaming");
        if (streaming.Exists())
        {
            BindStreaming(options.Streaming, streaming);
        }
    }

    private static void BindStreaming(IbkrStreamingOptions options, IConfiguration configuration)
    {
        if (configuration["Address"] is { Length: > 0 } address)
        {
            options.Address = Uri.TryCreate(address, UriKind.Absolute, out var uri)
                ? uri
                : throw new InvalidOperationException(
                    $"'{address}' is not a valid absolute URI for Streaming.{nameof(IbkrStreamingOptions.Address)}.");
        }

        if (configuration["Origin"] is { Length: > 0 } origin)
        {
            options.Origin = origin;
        }

        if (TryParseDuration(configuration["KeepAliveInterval"], out var keepAliveInterval))
        {
            options.KeepAliveInterval = keepAliveInterval;
        }

        if (bool.TryParse(configuration["Reconnect"], out var reconnect))
        {
            options.Reconnect = reconnect;
        }

        if (TryParseDuration(configuration["ReconnectDelay"], out var reconnectDelay))
        {
            options.ReconnectDelay = reconnectDelay;
        }

        if (TryParseDuration(configuration["ReconnectMaxDelay"], out var reconnectMaxDelay))
        {
            options.ReconnectMaxDelay = reconnectMaxDelay;
        }

        if (int.TryParse(configuration["BufferCapacity"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bufferCapacity))
        {
            options.BufferCapacity = bufferCapacity;
        }

        if (Enum.TryParse<StreamingOverflowMode>(configuration["Overflow"], ignoreCase: true, out var overflow))
        {
            options.Overflow = overflow;
        }

        if (TryParseDuration(configuration["CloseTimeout"], out var closeTimeout))
        {
            options.CloseTimeout = closeTimeout;
        }

        if (TryParseDuration(configuration["MarketDataRenewalInterval"], out var renewalInterval))
        {
            options.MarketDataRenewalInterval = renewalInterval;
        }
    }

    /// <summary>
    /// Reads a duration written as a <c>TimeSpan</c>, in NodaTime's round-trip form, or as a whole
    /// number of seconds.
    /// </summary>
    internal static bool TryParseDuration(string? value, out Duration duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // A bare number is checked first and read as seconds. Both NodaTime's round-trip pattern
        // and TimeSpan.TryParse would otherwise read "90" as ninety days.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            duration = Duration.FromSeconds(seconds);
            return true;
        }

        var roundtrip = DurationPattern.Roundtrip.Parse(value);
        if (roundtrip.Success)
        {
            duration = roundtrip.Value;
            return true;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
        {
            duration = Duration.FromTimeSpan(timeSpan);
            return true;
        }

        return false;
    }
}
