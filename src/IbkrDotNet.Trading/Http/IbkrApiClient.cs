using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IbkrDotNet.Trading.Configuration;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Serialization;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace IbkrDotNet.Trading.Http;

/// <summary>
/// The default <see cref="IIbkrApiClient"/>, layered over a configured <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// Rate limiting is applied here rather than by a <see cref="DelegatingHandler"/> in the pipeline.
/// <see cref="HttpClient.Timeout"/> covers the whole handler chain, so a wait taken inside it is
/// charged to the caller's request budget: pacing a request for the default thirty-second
/// <see cref="IbkrRateLimitingOptions.MaxWait"/> would exhaust the default thirty-second timeout and
/// report a timeout for a request that was never sent. Waiting out here costs nothing, and the
/// timeout goes back to measuring only what IBKR is responsible for.
/// </remarks>
public sealed class IbkrApiClient : IIbkrApiClient
{
    /// <summary>The name used when resolving this client's <see cref="HttpClient"/> by name.</summary>
    public const string HttpClientName = "IbkrDotNet.Trading";

    private const int MaxLoggedBodyLength = 2048;

    private static readonly MediaTypeHeaderValue JsonMediaType =
        new("application/json") { CharSet = "utf-8" };

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly IbkrRateLimiterRegistry? _rateLimiters;
    private readonly ILogger<IbkrApiClient> _logger;

    /// <summary>Creates the client over a single HTTP client.</summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <remarks>
    /// Suited to tests and to short-lived callers that construct the pipeline by hand. Long-running
    /// hosts should prefer the <c>Func&lt;HttpClient&gt;</c> overload, so the underlying handler can
    /// be rotated.
    /// </remarks>
    public IbkrApiClient(HttpClient httpClient, ILogger<IbkrApiClient> logger)
        : this(httpClient, rateLimiters: null, logger)
    {
    }

    /// <summary>Creates the client over a single HTTP client, pacing requests as it sends them.</summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="rateLimiters">
    /// The shared limiters, or <see langword="null"/> to send without pacing. Pass the singleton:
    /// the windows are the state, and a per-call registry would start every one of them afresh.
    /// </param>
    /// <param name="logger">The logger.</param>
    public IbkrApiClient(
        HttpClient httpClient,
        IbkrRateLimiterRegistry? rateLimiters,
        ILogger<IbkrApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = () => httpClient;
        _rateLimiters = rateLimiters;
        _logger = logger;
    }

    /// <summary>Creates the client over a factory that supplies an HTTP client per request.</summary>
    /// <param name="httpClientFactory">Supplies the HTTP client for each request.</param>
    /// <param name="logger">The logger.</param>
    /// <remarks>
    /// This is what the dependency injection package uses, passing
    /// <c>() =&gt; IHttpClientFactory.CreateClient(<see cref="HttpClientName"/>)</c>. A singleton
    /// holding one resolved client would pin the handler chain built at startup, so the connection
    /// pool would never pick up a DNS change — the long-lived <see cref="HttpClient"/> problem
    /// <c>IHttpClientFactory</c> exists to solve. Resolving per request is the cheap path: the
    /// client it returns is a thin wrapper over a pooled handler.
    /// </remarks>
    public IbkrApiClient(Func<HttpClient> httpClientFactory, ILogger<IbkrApiClient> logger)
        : this(httpClientFactory, rateLimiters: null, logger)
    {
    }

    /// <summary>
    /// Creates the client over a factory that supplies an HTTP client per request, pacing requests
    /// as it sends them. This is the constructor the dependency injection package uses.
    /// </summary>
    /// <param name="httpClientFactory">Supplies the HTTP client for each request.</param>
    /// <param name="rateLimiters">
    /// The shared limiters, or <see langword="null"/> to send without pacing. Pass the singleton:
    /// the windows are the state, and a per-call registry would start every one of them afresh.
    /// </param>
    /// <param name="logger">The logger.</param>
    public IbkrApiClient(
        Func<HttpClient> httpClientFactory,
        IbkrRateLimiterRegistry? rateLimiters,
        ILogger<IbkrApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _rateLimiters = rateLimiters;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> SendAsync<TResponse>(
        IbkrRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAndEnsureSuccessAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new IbkrApiException(
                $"{request} succeeded with {(int)response.StatusCode} but returned an empty body, " +
                $"where a {typeof(TResponse).Name} was expected.")
            {
                StatusCode = response.StatusCode,
                Method = request.Method.Method,
                Path = request.Path,
            };
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(body, IbkrJson.Options)
                ?? throw new IbkrApiException($"{request} returned a JSON null where a value was expected.")
                {
                    StatusCode = response.StatusCode,
                    Method = request.Method.Method,
                    Path = request.Path,
                    ResponseBody = Truncate(body),
                };
        }
        catch (JsonException ex)
        {
            throw new IbkrApiException(
                $"{request} returned a body that could not be read as {typeof(TResponse).Name}: {ex.Message}",
                ex)
            {
                StatusCode = response.StatusCode,
                Method = request.Method.Method,
                Path = request.Path,
                ResponseBody = Truncate(body),
            };
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(IbkrRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAndEnsureSuccessAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendRawAsync(
        IbkrRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await PaceAsync(request, cancellationToken).ConfigureAwait(false);
        return await _httpClientFactory()
            .SendAsync(BuildMessage(request), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(
        IbkrRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PaceAsync(request, cancellationToken).ConfigureAwait(false);

        IbkrLog.SendingRequest(_logger, request.Method.Method, request.Path);

        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory()
                .SendAsync(BuildMessage(request), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new IbkrApiException($"{request} could not be sent: {ex.Message}", ex)
            {
                Method = request.Method.Method,
                Path = request.Path,
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IbkrApiException($"{request} timed out.", ex)
            {
                Method = request.Method.Method,
                Path = request.Path,
            };
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateFailure(request, response, body);
        }
    }

    /// <summary>
    /// Waits for the limits that apply to a request, before the send that
    /// <see cref="HttpClient.Timeout"/> is measuring begins.
    /// </summary>
    private Task PaceAsync(IbkrRequest request, CancellationToken cancellationToken) =>
        _rateLimiters is null
            ? Task.CompletedTask
            : _rateLimiters.AcquireAsync(
                request.Method,
                new Uri(request.ToRelativeUri(), UriKind.Relative),
                cancellationToken);

    private static HttpRequestMessage BuildMessage(IbkrRequest request)
    {
        var message = new HttpRequestMessage(request.Method, request.ToRelativeUri());
        if (request.Body is not null)
        {
            // Serialized up front, rather than handed to JsonContent, so that the request carries a
            // Content-Length. JsonContent cannot report its length, so HttpClient falls back to
            // chunked transfer encoding, and the edge in front of IBKR answers a chunked request
            // with 411 Length Required before it ever reaches the API.
            var json = JsonSerializer.SerializeToUtf8Bytes(request.Body, request.Body.GetType(), IbkrJson.Options);
            message.Content = new ByteArrayContent(json);
            message.Content.Headers.ContentType = JsonMediaType;
        }

        return message;
    }

    private static IbkrApiException CreateFailure(
        IbkrRequest request,
        HttpResponseMessage response,
        string body)
    {
        var truncated = Truncate(body);
        var status = response.StatusCode;

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new IbkrAuthenticationException(
                $"{request} was rejected with {(int)status} {status}. On an /iserver endpoint this " +
                "usually means the brokerage session has lapsed rather than that the credentials " +
                $"are wrong. Response body: {truncated}")
            {
                StatusCode = status,
                Method = request.Method.Method,
                Path = request.Path,
                ResponseBody = truncated,
            };
        }

        if (status == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = ReadRetryAfter(response);
            return new IbkrRateLimitExceededException(
                $"{request} was rejected with 429 Too Many Requests. Repeated breaches can get the " +
                $"calling IP address blocked by IBKR. Response body: {truncated}")
            {
                StatusCode = status,
                Method = request.Method.Method,
                Path = request.Path,
                ResponseBody = truncated,
                RetryAfter = retryAfter,
            };
        }

        return new IbkrApiException($"{request} failed with {(int)status} {status}: {truncated}")
        {
            StatusCode = status,
            Method = request.Method.Method,
            Path = request.Path,
            ResponseBody = truncated,
        };
    }

    private static Duration? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return Duration.FromTimeSpan(delta);
        }

        return null;
    }

    private static string Truncate(string body) =>
        body.Length <= MaxLoggedBodyLength
            ? body
            : string.Concat(body.AsSpan(0, MaxLoggedBodyLength), "... (truncated)");
}

internal static partial class IbkrLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Sending {Method} {Path} to the IBKR Web API.")]
    public static partial void SendingRequest(ILogger logger, string method, string path);
}
