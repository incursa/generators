namespace Incursa.Generators.AppDefinitions.Pipeline;

using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Emission;

public sealed record GenerationResult(
    GenerationExecutionMode Mode,
    IReadOnlyList<GeneratorDiagnostic> Diagnostics,
    IReadOnlyList<GeneratedFile> GeneratedFiles,
    int DiscoveredFeatureCount,
    int MatchedFeatureCount,
    int FilesWritten,
    int FilesDeleted,
    int FilesUnchanged)
{
    public bool Success => Diagnostics.All(static diagnostic => diagnostic.Severity != GeneratorDiagnosticSeverity.Error);
}
