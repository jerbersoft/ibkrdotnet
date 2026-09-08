using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Models.Scanner;

namespace IbkrDotNet.Trading.Clients;

/// <summary>
/// The market Scanner endpoints, which select contracts by a ranked criterion.
/// </summary>
public interface IScannerClient
{
    /// <summary>
    /// Reads everything a scanner request can be built from: the scan types, instrument types,
    /// filters and locations.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IbkrRateLimitExceededException">
    /// A previous call was made less than fifteen minutes ago. This is the tightest limit in the
    /// Trading API, and one the client refuses to wait out: fifteen minutes is far longer than the
    /// maximum wait it will block a request for.
    /// </exception>
    /// <remarks>
    /// Around 200 KB of reference data that changes rarely, behind a limit of one request per
    /// fifteen minutes. Fetch it once and hold it; do not call this per scan.
    /// </remarks>
    Task<ScannerParameters> GetParametersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a market scanner and returns the contracts it matched.
    /// </summary>
    /// <param name="instrument">
    /// The instrument type to scan, from <see cref="ScannerInstrument.Type"/>, for example <c>STK</c>.
    /// </param>
    /// <param name="scanType">
    /// The criterion to rank by, from <see cref="ScannerType.Code"/>, for example
    /// <c>TOP_PERC_GAIN</c>.
    /// </param>
    /// <param name="location">
    /// The market to scan, from <see cref="ScannerLocation.Type"/>, for example
    /// <c>STK.US.MAJOR</c>.
    /// </param>
    /// <param name="filters">Additional filters to narrow the scan.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>
    /// A scan selects contracts; it is not a source of market data. The value each contract was
    /// ranked by is often absent from the response -- see <see cref="ScannerContract.ScanData"/> --
    /// so read the quotes for the returned conids if the numbers matter.
    /// </remarks>
    Task<ScannerResults> RunAsync(
        string instrument,
        string scanType,
        string? location = null,
        IEnumerable<ScannerFilter>? filters = null,
        CancellationToken cancellationToken = default);
}
