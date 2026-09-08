using System.Runtime.CompilerServices;
using Xunit;

namespace IbkrDotNet.Tests.Shared;

/// <summary>
/// Where the repository is, for the tests that read files out of it rather than out of the build
/// output.
/// </summary>
/// <remarks>
/// Located from the calling file's compile-time path rather than from the working directory, which a
/// test runner chooses and this cannot: walk up from the caller until a directory holds
/// <c>IbkrDotNet.slnx</c>. The same reason <see cref="PublicApiApproval"/> takes a
/// <see cref="CallerFilePathAttribute"/>.
/// </remarks>
internal static class RepositoryLayout
{
    /// <summary>The repository root.</summary>
    /// <param name="here">Supplied by the compiler.</param>
    public static string Root([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IbkrDotNet.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
