namespace Incursa.Generators.AppDefinitions.Config;

public sealed class GeneratorConfig
{
    public int Version { get; init; } = 1;

    public string? DefinitionRoot { get; init; }

    public IReadOnlyList<string>? DefinitionPatterns { get; init; }

    public ValidationSettings Validation { get; init; } = new();

    public Dictionary<string, OutputTargetConfig> Targets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ValidationSettings
{
    public IReadOnlyList<string> KnownTypeNames { get; init; } = [];

    public bool AllowUnqualifiedExternalTypes { get; init; }
}

public sealed class OutputTargetConfig
{
    public string? Kind { get; init; }

    public string? Directory { get; init; }

    public string? Namespace { get; init; }

    public bool PreserveDefinitionFolders { get; init; } = true;

    public bool AppendRelativePathToNamespace { get; init; }

    public IReadOnlyDictionary<string, string> Imports { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record ResolvedGeneratorConfig(
    string ConfigFilePath,
    string DefinitionRoot,
    IReadOnlyList<string> DefinitionPatterns,
    ValidationSettings Validation,
    IReadOnlyList<ResolvedOutputTarget> Targets);

public sealed record ResolvedOutputTarget(
    string Name,
    string Kind,
    string Directory,
    string Namespace,
    bool PreserveDefinitionFolders,
    bool AppendRelativePathToNamespace,
    IReadOnlyDictionary<string, string> Imports);
