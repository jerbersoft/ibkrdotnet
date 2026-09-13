using System.Runtime.CompilerServices;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Models.MarketData;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IMarketDataStreamClient" />
public sealed class MarketDataStreamClient : IMarketDataStreamClient
{
    private readonly IIbkrStreamingTransport _transport;
    private readonly IbkrStreamingOptions _options;
    private readonly ILogger<MarketDataStreamClient> _logger;

    /// <summary>Creates the client over a transport.</summary>
    /// <param name="transport">The socket the stream runs over.</param>
    /// <param name="options">The client options; <see cref="IbkrTradingOptions.Streaming"/> is what is read.</param>
    /// <param name="logger">Where subscriptions and renewals are logged.</param>
    public MarketDataStreamClient(
        IIbkrStreamingTransport transport,
        IOptions<IbkrTradingOptions> options,
        ILogger<MarketDataStreamClient> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _options = options.Value.Streaming;
        _options.Validate();
        _logger = logger;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<MarketDataUpdate> SubscribeAsync(
        ConId conId,
        IReadOnlyList<string>? fields = null,
        string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        // Validated here so a bad argument throws at the call, not at the first read.
        var request = CreateRequest(conId, fields, exchange);
        return StreamAsync(request, cancellationToken);
    }

    /// <summary>
    /// Builds the <c>smd</c> request for an instrument: the target is the contract identifier,
    /// with <c>@EXCHANGE</c> appended when a data source is named, and the fields ride as JSON
    /// strings, which is how IBKR insists on them.
    /// </summary>
    internal static StreamingSubscriptionRequest CreateRequest(
        ConId conId,
        IReadOnlyList<string>? fields,
        string? exchange)
    {
        fields ??= MarketDataField.TopOfBook;
        if (fields.Count == 0)
        {
            throw new ArgumentException(
                "At least one field is required. Pass null to stream " +
                $"{nameof(MarketDataField)}.{nameof(MarketDataField.TopOfBook)}.",
                nameof(fields));
        }

        if (fields.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A field tag cannot be blank.", nameof(fields));
        }

        if (exchange is not null && !IsFramable(exchange))
        {
            throw new ArgumentException(
                "The exchange cannot be blank or contain whitespace or '+'. Pass null for SMART.",
                nameof(exchange));
        }

        var target = exchange is null
            ? conId.ToString()
            : $"{conId}@{exchange}";

        return StreamingSubscriptionRequest.Solicited(StreamingTopics.MarketData, target, new { fields });
    }

    private static bool IsFramable(string exchange) =>
        exchange.Length > 0
        && !exchange.Any(c => char.IsWhiteSpace(c) || c == StreamingFrame.Separator);

    private async IAsyncEnumerable<MarketDataUpdate> StreamAsync(
        StreamingSubscriptionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var target = request.Target!;

        await using var subscription = await _transport.SubscribeAsync(request, cancellationToken).ConfigureAwait(false);
        MarketDataStreamLog.Subscribed(_logger, target);

        // Renewal runs beside the read, on its own token, so ending the read ends it too.
        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewal = RenewAsync(request, renewalCts.Token);
        try
        {
            await foreach (var message in subscription.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return message.Deserialize<MarketDataUpdate>() with { ReceivedAt = message.ReceivedAt };
            }
        }
        finally
        {
            await renewalCts.CancelAsync().ConfigureAwait(false);
            await renewal.ConfigureAwait(false);
            MarketDataStreamLog.Unsubscribing(_logger, target);
        }
    }

    /// <summary>
    /// Re-sends the request on the renewal interval. IBKR ends a stream 15 minutes after its last
    /// request, so a stream read for longer than that has to be asked for again.
    /// </summary>
    private async Task RenewAsync(StreamingSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var frame = request.SubscribeFrame!;
        var target = request.Target!;

        try
        {
            using var timer = new PeriodicTimer(_options.MarketDataRenewalInterval.ToTimeSpan());
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _transport.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    MarketDataStreamLog.Renewed(_logger, target);
                }
                catch (IbkrStreamingException ex)
                {
                    // The socket is down or refused the frame. If it is reconnecting, the transport
                    // re-sends the request itself when it comes back; the next tick tries again either way.
                    MarketDataStreamLog.RenewalFailed(_logger, target, ex);
                }
                catch (ObjectDisposedException)
                {
                    // The transport is gone, and the read loop is ending with it.
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The stream ended.
        }
    }
}

/// <summary>Log messages for the market data stream client. Event identifiers 1320–1329.</summary>
internal static partial class MarketDataStreamLog
{
    [LoggerMessage(EventId = 1320, Level = LogLevel.Debug, Message = "Streaming market data for {Target}.")]
    public static partial void Subscribed(ILogger logger, string target);

    [LoggerMessage(EventId = 1321, Level = LogLevel.Debug, Message = "Re-requested market data for {Target}; IBKR ends a stream 15 minutes after its last request.")]
    public static partial void Renewed(ILogger logger, string target);

    [LoggerMessage(EventId = 1322, Level = LogLevel.Warning, Message = "Market data for {Target} could not be re-requested. The stream ends 15 minutes after its last request unless the socket reconnects first.")]
    public static partial void RenewalFailed(ILogger logger, string target, Exception exception);

    [LoggerMessage(EventId = 1323, Level = LogLevel.Debug, Message = "Ending the market data stream for {Target}.")]
    public static partial void Unsubscribing(ILogger logger, string target);
}
