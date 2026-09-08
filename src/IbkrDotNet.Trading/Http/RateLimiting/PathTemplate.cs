namespace IbkrDotNet.Trading.Http.RateLimiting;

/// <summary>
/// Matches a request path against a template such as
/// <c>/v1/api/fyi/notifications/{notificationId}</c>, where a <c>{name}</c> segment matches any
/// single segment.
/// </summary>
internal sealed class PathTemplate
{
    private readonly string[] _segments;

    private PathTemplate(string template, string[] segments)
    {
        Template = template;
        _segments = segments;
    }

    public string Template { get; }

    public static PathTemplate Parse(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        return new PathTemplate(
            template,
            template.Split('/', StringSplitOptions.RemoveEmptyEntries));
    }

    public bool Matches(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var index = 0;
        foreach (var range in Split(path))
        {
            if (index >= _segments.Length)
            {
                return false;
            }

            var expected = _segments[index];
            var actual = path.AsSpan(range.Start, range.Length);

            // A '{name}' segment matches any single, non-empty segment.
            var isPlaceholder = expected.Length > 1 && expected[0] == '{' && expected[^1] == '}';
            if (!isPlaceholder && !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            index++;
        }

        return index == _segments.Length;
    }

    private static IEnumerable<(int Start, int Length)> Split(string path)
    {
        var start = 0;
        while (start < path.Length)
        {
            if (path[start] == '/')
            {
                start++;
                continue;
            }

            var end = path.IndexOf('/', start);
            if (end < 0)
            {
                end = path.Length;
            }

            yield return (start, end - start);
            start = end;
        }
    }

    public override string ToString() => Template;
}
