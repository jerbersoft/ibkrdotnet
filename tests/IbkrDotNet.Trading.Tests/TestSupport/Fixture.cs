using System.Reflection;
using System.Text.Json;

namespace IbkrDotNet.Trading.Tests.TestSupport;

/// <summary>
/// Loads the JSON fixtures embedded in this assembly.
/// </summary>
/// <remarks>
/// Response fixtures are the example payloads published in IBKR's own endpoint reference, so
/// deserialization is checked against what the API actually emits rather than against an assumption
/// about it.
/// </remarks>
public static class Fixture
{
    private const string Prefix = "IbkrDotNet.Trading.Tests.Fixtures.";

    public static string ReadText(string relativePath)
    {
        // MSBuild turns '-' into '_' in the directory portion of a resource name but leaves the
        // file name alone, so 'trading-session/logout.json' becomes 'trading_session.logout.json'.
        var segments = relativePath.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            segments[i] = segments[i].Replace('-', '_');
        }

        var name = Prefix + string.Join('.', segments);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Fixture '{relativePath}' was not found. Embedded resources: " +
                string.Join(", ", Assembly.GetExecutingAssembly().GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static JsonDocument ReadJson(string relativePath) => JsonDocument.Parse(ReadText(relativePath));
}
