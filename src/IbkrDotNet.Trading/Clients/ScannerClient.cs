using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Scanner;

namespace IbkrDotNet.Trading.Clients;

/// <inheritdoc cref="IScannerClient" />
public sealed class ScannerClient(IIbkrApiClient apiClient) : IScannerClient
{
    private readonly IIbkrApiClient _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));

    /// <inheritdoc />
    public Task<ScannerParameters> GetParametersAsync(CancellationToken cancellationToken = default) =>
        _apiClient.SendAsync<ScannerParameters>(
            IbkrRequest.Get("/v1/api/iserver/scanner/params"),
            cancellationToken);

    /// <inheritdoc />
    public Task<ScannerResults> RunAsync(
        string instrument,
        string scanType,
        string? location = null,
        IEnumerable<ScannerFilter>? filters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument);
        ArgumentException.ThrowIfNullOrWhiteSpace(scanType);

        // Sent even when empty: IBKR documents 'filter' as optional, but the shape it accepts
        // without complaint is the one its own examples send.
        var applied = filters?.ToArray() ?? [];

        return _apiClient.SendAsync<ScannerResults>(
            IbkrRequest.Post("/v1/api/iserver/scanner/run")
                .WithJsonBody(new ScannerRequest(instrument, scanType, location, applied)),
            cancellationToken);
    }
}
