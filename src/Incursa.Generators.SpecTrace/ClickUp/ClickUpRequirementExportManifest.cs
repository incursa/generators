namespace Incursa.Generators.SpecTrace.ClickUp;

public sealed record ClickUpRequirementExportManifest(
    string Repository,
    string? SourceCommit,
    DateTimeOffset GeneratedAt,
    string CsvPath,
    int TotalRequirements,
    IReadOnlyList<string> SkippedFiles,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ClickUpRequirementExportManifestRow> Rows);
