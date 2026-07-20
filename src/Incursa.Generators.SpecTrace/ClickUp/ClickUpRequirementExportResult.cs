namespace Incursa.Generators.SpecTrace.ClickUp;

public sealed record ClickUpRequirementExportResult(
    bool Success,
    string CsvPath,
    string ManifestPath,
    int TotalRequirements,
    IReadOnlyList<string> SkippedFiles,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
