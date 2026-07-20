namespace Incursa.Generators.SpecTrace.ClickUp;

public sealed record ClickUpRequirementExportManifestRow(
    string SpecTraceId,
    string SourceSpec,
    string SourceFile,
    string? SourceCommit,
    string TaskName,
    int RowNumber,
    string StableFingerprint);
