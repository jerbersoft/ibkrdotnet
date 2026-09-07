using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IbkrDotNet.Trading.Serialization;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace IbkrDotNet.Trading.Http;

/// <summary>
/// The default <see cref="IIbkrApiClient"/>, layered over a configured <see cref="HttpClient"/>.
/// </summary>
public sealed class IbkrApiClient : IIbkrApiClient
{
    /// <summary>The name used when resolving this client's <see cref="HttpClient"/> by name.</summary>
    public const string HttpClientName = "IbkrDotNet.Trading";

    private const int MaxLoggedBodyLength = 2048;

    private readonly HttpClient _httpClient;
    private readonly ILogger<IbkrApiClient> _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public IbkrApiClient(HttpClient httpClient, ILogger<IbkrApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
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
    public Task<HttpResponseMessage> SendRawAsync(
        IbkrRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _httpClient.SendAsync(
            BuildMessage(request),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(
        IbkrRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IbkrLog.SendingRequest(_logger, request.Method.Method, request.Path);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
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

    private static HttpRequestMessage BuildMessage(IbkrRequest request)
    {
        var message = new HttpRequestMessage(request.Method, request.ToRelativeUri());
        if (request.Body is not null)
        {
            message.Content = JsonContent.Create(request.Body, request.Body.GetType(), options: IbkrJson.Options);
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
