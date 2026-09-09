using System.Text.RegularExpressions;
using IbkrDotNet.Tests.Shared;
using Xunit;

namespace IbkrDotNet.Trading.Tests;

/// <summary>
/// Pins the facts about publishing that only fail once a package is already on nuget.org, where
/// nothing can be taken back.
/// </summary>
/// <remarks>
/// <para>
/// The release workflow pushes one file per id named in its <c>PACKAGES</c> list, in that order, so
/// the list governs what ships rather than describing it. A new project under <c>src/</c> that
/// nobody adds to it would pack and then fail the run; a stale entry would fail it too. Either is
/// better found here than at a tag.
/// </para>
/// <para>
/// The Source Link guard is the one that has already cost something. The .NET SDK has carried
/// Source Link in-box since .NET 8; referencing <c>Microsoft.SourceLink.GitHub</c> explicitly
/// replaces the SDK's copy with one that does not enable the source-control queries, and the
/// failure is invisible — the build is green, the package is valid, and only its nuspec's missing
/// commit says that nobody will ever step into the source. 0.1.0 and 0.2.0 both shipped that way.
/// </para>
/// <para>
/// Both tests read the files rather than running MSBuild, so neither needs a build to have happened.
/// </para>
/// </remarks>
public class PackagingTests
{
    private static string ReleaseWorkflow =>
        File.ReadAllText(Path.Combine(RepositoryLayout.Root(), ".github", "workflows", "release.yml"));

    [Fact]
    public void Every_shipping_project_is_named_in_the_release_workflow()
    {
        var src = Path.Combine(RepositoryLayout.Root(), "src");
        var onDisk = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(PackageIdOf)
            .Order(StringComparer.Ordinal)
            .ToList();

        var declared = Regex.Match(ReleaseWorkflow, @"^\s*PACKAGES:\s*(?<ids>.+)$", RegexOptions.Multiline);
        Assert.True(declared.Success, "release.yml no longer declares a PACKAGES list.");

        var published = declared.Groups["ids"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk, published);
    }

    [Fact]
    public void The_core_package_is_pushed_before_the_package_that_depends_on_it()
    {
        // Pushed in list order. Reversing them would leave a window -- and, if the second push is
        // refused, a permanent state -- where the feed carries a package declaring a dependency on
        // a version that is not there.
        var declared = Regex.Match(ReleaseWorkflow, @"^\s*PACKAGES:\s*(?<ids>.+)$", RegexOptions.Multiline);
        var published = declared.Groups["ids"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        Assert.True(
            published.IndexOf("IbkrDotNet.Trading")
                < published.IndexOf("IbkrDotNet.Extensions.DependencyInjection"),
            $"PACKAGES pushes in the order it lists, and this order publishes the dependent package "
            + $"first: {string.Join(", ", published)}.");
    }

    [Fact]
    public void Nothing_references_Microsoft_SourceLink_GitHub_explicitly()
    {
        var root = RepositoryLayout.Root();
        var offenders = Directory
            .GetFiles(root, "*.props", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("Include=\"Microsoft.SourceLink.GitHub\"", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These reference Microsoft.SourceLink.GitHub explicitly, which replaces the SDK's in-box Source Link "
            + $"and silently stops the commit being recorded in the packed nuspec: {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void The_build_publishes_the_repository_url_so_the_commit_is_recorded()
    {
        var props = File.ReadAllText(Path.Combine(RepositoryLayout.Root(), "Directory.Build.props"));

        Assert.Contains("<PublishRepositoryUrl>true</PublishRepositoryUrl>", props, StringComparison.Ordinal);
    }

    private static string PackageIdOf(string csproj)
    {
        var id = Regex.Match(File.ReadAllText(csproj), @"<PackageId>(?<id>[^<]+)</PackageId>");
        return id.Success ? id.Groups["id"].Value : Path.GetFileNameWithoutExtension(csproj);
    }
}
