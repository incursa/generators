namespace Incursa.Generators.AppDefinitions.Tests.Fixtures;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "incursa-appdefinitions-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public static string GetTestDataPath(params string[] segments)
    {
        return Path.Combine([AppContext.BaseDirectory, "TestData", .. segments]);
    }

    public static string GetRepositoryPath(params string[] segments)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine([repositoryRoot, .. segments]);
    }

    public void CopyDirectory(string sourcePath, string destinationRelativePath = "")
    {
        var destinationPath = Path.Combine(RootPath, destinationRelativePath);
        CopyDirectoryContents(sourcePath, destinationPath);
    }

    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content.Replace("\r\n", "\n", StringComparison.Ordinal));
        return fullPath;
    }

    public static IReadOnlyDictionary<string, string> ReadFiles(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetRelativePath(rootPath, path).Replace(Path.DirectorySeparatorChar, '/'),
                path => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal),
                StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static void CopyDirectoryContents(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destinationFile = Path.Combine(destinationPath, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destinationFile, overwrite: true);
        }
    }
}
