namespace Incursa.Generators.SpecTrace.ClickUp;

public sealed record ClickUpRequirementExportRequest(
    string RepositoryRoot,
    string Repository,
    string? SourceCommit,
    string? OutputPath,
    string? Filter,
    DateOnly LastSync,
    string? ExcludeFilter = null,
    bool SkipLifecycleStateDerivation = false);
