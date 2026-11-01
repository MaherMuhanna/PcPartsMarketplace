namespace PCM.ArchitectureTests;

/// <summary>Finds the repository root (the folder with PcPartsMarketplace.slnx) and reads source files from it.</summary>
internal static class SourceTree
{
    private const string SolutionFile = "PcPartsMarketplace.slnx";

    public static string Root { get; } = FindRoot();

    /// <summary>Every C# file under a project folder, skipping build output.</summary>
    public static IEnumerable<string> CSharpFiles(string projectFolder) =>
        Directory.EnumerateFiles(Path.Combine(Root, projectFolder), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    /// <summary>Path relative to the repository root, with forward slashes (stable test messages).</summary>
    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}") || path.Contains($"{separator}obj{separator}");
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"{SolutionFile} not found above {AppContext.BaseDirectory}.");
    }
}
