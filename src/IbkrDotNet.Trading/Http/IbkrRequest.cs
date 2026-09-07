using System.Collections.ObjectModel;
using System.Globalization;

namespace IbkrDotNet.Trading.Http;

/// <summary>
/// A single request to the Interactive Brokers Web API, built fluently.
/// </summary>
/// <remarks>
/// Endpoint clients build these internally, but the type is public so callers can reach endpoints
/// this library has not modelled yet through <see cref="IIbkrApiClient"/>. An instance describes one
/// request and is not intended to be shared or reused.
/// </remarks>
public sealed class IbkrRequest
{
    private readonly Dictionary<string, string> _query = [];
    private readonly ReadOnlyDictionary<string, string> _queryView;

    private IbkrRequest(HttpMethod method, string path)
    {
        Method = method;
        Path = path;
        _queryView = new ReadOnlyDictionary<string, string>(_query);
    }

    /// <summary>The HTTP method.</summary>
    public HttpMethod Method { get; }

    /// <summary>
    /// The request path, including the <c>/v1/api</c> prefix, for example
    /// <c>/v1/api/iserver/accounts</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>The query string parameters, in insertion order.</summary>
    public IReadOnlyDictionary<string, string> Query => _queryView;

    /// <summary>The object to serialize as a JSON request body, when there is one.</summary>
    public object? Body { get; private set; }

    /// <summary>Creates a <c>GET</c> request.</summary>
    /// <param name="path">The request path, including the <c>/v1/api</c> prefix.</param>
    public static IbkrRequest Get(string path) => new(HttpMethod.Get, Validate(path));

    /// <summary>Creates a <c>POST</c> request.</summary>
    /// <param name="path">The request path, including the <c>/v1/api</c> prefix.</param>
    public static IbkrRequest Post(string path) => new(HttpMethod.Post, Validate(path));

    /// <summary>Creates a <c>PUT</c> request.</summary>
    /// <param name="path">The request path, including the <c>/v1/api</c> prefix.</param>
    public static IbkrRequest Put(string path) => new(HttpMethod.Put, Validate(path));

    /// <summary>Creates a <c>DELETE</c> request.</summary>
    /// <param name="path">The request path, including the <c>/v1/api</c> prefix.</param>
    public static IbkrRequest Delete(string path) => new(HttpMethod.Delete, Validate(path));

    /// <summary>Escapes a value for safe interpolation into a request path.</summary>
    /// <param name="value">The value to escape.</param>
    public static string PathSegment(string value) => Uri.EscapeDataString(value);

    /// <summary>Renders a numeric value for interpolation into a request path.</summary>
    /// <param name="value">The value to render.</param>
    public static string PathSegment(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Adds a query parameter. A <see langword="null"/> value is ignored.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value, or <see langword="null"/> to omit it.</param>
    public IbkrRequest WithQuery(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (value is not null)
        {
            _query[name] = value;
        }

        return this;
    }

    /// <summary>Adds a numeric query parameter. A <see langword="null"/> value is ignored.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value, or <see langword="null"/> to omit it.</param>
    public IbkrRequest WithQuery(string name, long? value) =>
        WithQuery(name, value?.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a boolean query parameter, rendered as <c>true</c> or <c>false</c>. A
    /// <see langword="null"/> value is ignored.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value, or <see langword="null"/> to omit it.</param>
    public IbkrRequest WithQuery(string name, bool? value) =>
        WithQuery(name, value is null ? null : value.Value ? "true" : "false");

    /// <summary>
    /// Adds a query parameter holding a comma-separated list. A null or empty sequence is ignored.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="values">The values to join with commas.</param>
    public IbkrRequest WithCommaSeparatedQuery(string name, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return this;
        }

        var joined = string.Join(',', values);
        return joined.Length == 0 ? this : WithQuery(name, joined);
    }

    /// <summary>Attaches an object to be serialized as the JSON request body.</summary>
    /// <param name="body">The body object.</param>
    public IbkrRequest WithJsonBody(object? body)
    {
        Body = body;
        return this;
    }

    /// <summary>The path and query string, ready to be resolved against a base address.</summary>
    public string ToRelativeUri()
    {
        if (_query.Count == 0)
        {
            return Path;
        }

        var parts = _query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");

        return $"{Path}?{string.Join('&', parts)}";
    }

    /// <inheritdoc />
    public override string ToString() => $"{Method.Method} {ToRelativeUri()}";

    private static string Validate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!path.StartsWith('/'))
        {
            throw new ArgumentException("Request paths must start with '/'.", nameof(path));
        }

        return path;
    }
}
