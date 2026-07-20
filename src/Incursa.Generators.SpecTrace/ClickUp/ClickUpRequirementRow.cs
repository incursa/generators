namespace Incursa.Generators.SpecTrace.ClickUp;

internal sealed record ClickUpRequirementRow(
    string TaskName,
    string TaskType,
    string AssignedTo,
    string Description,
    string SpecTraceId,
    string Repository,
    string SourceSpec,
    string GitHubSource,
    string ImplementationState,
    string VerificationState,
    string TraceStatus,
    string EvidenceState,
    string SourceSection,
    string SourceRefs,
    string? SourceCommit,
    string LastSync,
    string ArchitectureIds,
    string WorkItemIds,
    string VerificationIds,
    string SourceFile,
    string StableFingerprint);
