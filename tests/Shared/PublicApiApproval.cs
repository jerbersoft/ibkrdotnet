using System.Reflection;
using System.Runtime.CompilerServices;
using PublicApiGenerator;
using Xunit;

namespace IbkrDotNet.Tests.Shared;

/// <summary>
/// Compares a package's public surface against a checked-in transcript of it.
/// </summary>
/// <remarks>
/// <para>
/// Both packages are published, so every public signature is somebody's compile error once it
/// changes. Nothing else in the suite would notice: a widened parameter, a property that quietly
/// became nullable or a type that stopped being sealed all keep the tests green while breaking every
/// caller who already built against them. The transcript makes that visible in review, where it can
/// be a decision rather than an accident.
/// </para>
/// <para>
/// A deliberate change is meant to update the approved file. The failure writes the new surface
/// beside it as <c>.received.txt</c> so the diff can be read and the file replaced.
/// </para>
/// </remarks>
internal static class PublicApiApproval
{
    /// <summary>Asserts that an assembly's public surface matches its approved transcript.</summary>
    /// <param name="assembly">The assembly to transcribe.</param>
    /// <param name="callerFilePath">
    /// Supplied by the compiler, and the reason this works from either test project: the transcript
    /// lives next to the test that asks for it rather than at a path assembled from the build output.
    /// </param>
    public static void Approve(Assembly assembly, [CallerFilePath] string callerFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var actual = Normalize(assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            // Neither says anything about the surface a caller can see.
            ExcludeAttributes =
            [
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                "System.Reflection.AssemblyMetadataAttribute",
            ],
        }));

        var directory = Path.Combine(Path.GetDirectoryName(callerFilePath)!, "PublicApi");
        Directory.CreateDirectory(directory);

        var name = assembly.GetName().Name;
        var approvedPath = Path.Combine(directory, $"{name}.approved.txt");
        var receivedPath = Path.Combine(directory, $"{name}.received.txt");

        var approved = File.Exists(approvedPath) ? Normalize(File.ReadAllText(approvedPath)) : null;
        if (approved == actual)
        {
            // Leaving a stale received file behind would make a passing run look like a failing one.
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actual);
        Assert.Fail(approved is null
            ? $"{name} has no approved public API. The current surface has been written to " +
              $"{receivedPath}; rename it to {Path.GetFileName(approvedPath)} to accept it."
            : $"The public API of {name} has changed.\n\n{Describe(approved, actual)}\n" +
              $"If the change is intended, replace {Path.GetFileName(approvedPath)} with " +
              $"{Path.GetFileName(receivedPath)}, which has been written beside it.");
    }

    private static string Normalize(string api) =>
        api.ReplaceLineEndings("\n").TrimEnd() + "\n";

    /// <summary>
    /// Summarises the change as added and removed lines, since a whole-transcript diff of a few
    /// thousand members is not something anyone reads in test output.
    /// </summary>
    private static string Describe(string approved, string actual)
    {
        const int Shown = 15;

        var before = approved.Split('\n');
        var after = actual.Split('\n');

        var removed = before.Except(after, StringComparer.Ordinal).ToArray();
        var added = after.Except(before, StringComparer.Ordinal).ToArray();

        var lines = new List<string>();
        Append(lines, "Removed", removed, Shown);
        Append(lines, "Added", added, Shown);

        return lines.Count > 0
            ? string.Join('\n', lines)
            : "The lines are the same but their order is not.";

        static void Append(List<string> lines, string heading, string[] changes, int shown)
        {
            if (changes.Length == 0)
            {
                return;
            }

            lines.Add($"{heading} ({changes.Length}):");
            lines.AddRange(changes.Take(shown).Select(line => $"  {line.Trim()}"));
            if (changes.Length > shown)
            {
                lines.Add($"  ... and {changes.Length - shown} more");
            }
        }
    }
}
