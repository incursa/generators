namespace Incursa.Generators.SpecTrace.ClickUp;

using System.Globalization;
using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public sealed class ClickUpRequirementExporter
{
    private static readonly Regex RequirementIdRegex = new(
        @"\bREQ[-_][A-Z0-9]+(?:[-_][A-Z0-9]+)*[-_]\d{4,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly string[] CsvHeaders =
    [
        "Task Name",
        "Task type",
        "Assigned To",
        "Description",
        "SpecTrace ID",
        "Repository",
        "Source Spec",
        "GitHub Source",
        "Implementation State",
        "Verification State",
        "Trace Status",
        "Evidence State",
        "Source Section",
        "Source Refs",
        "Source Commit",
        "Last Sync",
        "Architecture IDs",
        "Work Item IDs",
        "Verification IDs",
    ];

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly string[] ImplementationStates =
    [
        "Not started",
        "Planned",
        "Partial",
        "Implemented",
        "Blocked",
        "Deferred",
        "Superseded",
        "Retired",
    ];

    private static readonly string[] VerificationStates =
    [
        "None",
        "Planned",
        "Passed",
        "Failed",
        "Blocked",
        "Waived",
        "Obsolete",
    ];

    public ClickUpRequirementExportResult Export(ClickUpRequirementExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var warnings = new List<string>();
        var skippedFiles = new List<string>();

        AddRequestValidationErrors(request, errors);
        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        if (!Directory.Exists(repositoryRoot))
        {
            errors.Add($"Repository root does not exist: {repositoryRoot}");
        }

        var outputPaths = ResolveOutputPaths(repositoryRoot, request.Repository, request.OutputPath);
        if (errors.Count > 0)
        {
            return new ClickUpRequirementExportResult(false, outputPaths.CsvPath, outputPaths.ManifestPath, 0, skippedFiles, warnings, errors);
        }

        var artifactIndex = ReadArtifactIndex(repositoryRoot);
        var evidenceIndex = request.SkipLifecycleStateDerivation
            ? RequirementEvidenceIndex.Empty
            : ReadRequirementEvidenceIndex(repositoryRoot);
        var rows = new List<ClickUpRequirementRow>();
        foreach (var file in EnumerateCandidateFiles(repositoryRoot, request.Filter, request.ExcludeFilter))
        {
            if (!TryReadSpecification(file, repositoryRoot, skippedFiles, warnings, out var specification))
            {
                continue;
            }

            rows.AddRange(CreateRows(request, repositoryRoot, file, specification, warnings, artifactIndex, evidenceIndex));
        }

        AddSourceRefSummaryWarning(rows, warnings);

        var duplicates = FindDuplicateRequirementIds(rows);
        if (duplicates.Count > 0)
        {
            errors.Add($"Duplicate requirement IDs found: {string.Join(", ", duplicates)}");
        }

        if (errors.Count > 0)
        {
            return new ClickUpRequirementExportResult(false, outputPaths.CsvPath, outputPaths.ManifestPath, rows.Count, skippedFiles, warnings, errors);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPaths.CsvPath)!);
        WriteCsv(outputPaths.CsvPath, rows);
        WriteManifest(outputPaths.ManifestPath, request, outputPaths.CsvPath, rows, skippedFiles, warnings);

        return new ClickUpRequirementExportResult(true, outputPaths.CsvPath, outputPaths.ManifestPath, rows.Count, skippedFiles, warnings, errors);
    }

    private static void AddRequestValidationErrors(ClickUpRequirementExportRequest request, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            errors.Add("Repository root is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Repository))
        {
            errors.Add("Repository full name is required.");
        }
    }

    private static IReadOnlyList<string> FindDuplicateRequirementIds(IEnumerable<ClickUpRequirementRow> rows)
    {
        return rows
            .GroupBy(static row => row.SpecTraceId, StringComparer.Ordinal)
            .Where(static group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddSourceRefSummaryWarning(IReadOnlyList<ClickUpRequirementRow> rows, List<string> warnings)
    {
        var rowsWithBlankSectionsAndRefs = rows.Count(static row => string.IsNullOrWhiteSpace(row.SourceSection) && !string.IsNullOrWhiteSpace(row.SourceRefs));
        if (rowsWithBlankSectionsAndRefs > 0)
        {
            warnings.Add($"{rowsWithBlankSectionsAndRefs} row(s) have blank Source Section but populated Source Refs.");
        }
    }

    private static (string CsvPath, string ManifestPath) ResolveOutputPaths(string repositoryRoot, string repository, string? outputPath)
    {
        var fallbackFileName = $"{SanitizeFileName(GetRepositoryName(repository))}.csv";
        string csvPath;
        string manifestPath;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var outputDirectory = Path.Combine(repositoryRoot, "artifacts", "clickup-requirements-import");
            csvPath = Path.Combine(outputDirectory, fallbackFileName);
            manifestPath = Path.Combine(outputDirectory, "requirements-import-manifest.json");
        }
        else
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            if (string.Equals(Path.GetExtension(fullOutputPath), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                csvPath = fullOutputPath;
                var directory = Path.GetDirectoryName(fullOutputPath)!;
                var manifestFileName = $"{Path.GetFileNameWithoutExtension(fullOutputPath)}.requirements-import-manifest.json";
                manifestPath = Path.Combine(directory, manifestFileName);
            }
            else
            {
                csvPath = Path.Combine(fullOutputPath, fallbackFileName);
                manifestPath = Path.Combine(fullOutputPath, "requirements-import-manifest.json");
            }
        }

        return (csvPath, manifestPath);
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string repositoryRoot, string? filter, string? excludeFilter)
    {
        var requirementsRoot = Path.Combine(repositoryRoot, "specs", "requirements");
        if (!Directory.Exists(requirementsRoot))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(requirementsRoot, "*.json", SearchOption.AllDirectories);
        if (!string.IsNullOrWhiteSpace(excludeFilter))
        {
            files = files.Where(file => !MatchesFilter(repositoryRoot, file, excludeFilter));
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return files.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return files
            .Where(file => MatchesFilter(repositoryRoot, file, filter))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesFilter(string repositoryRoot, string file, string filter)
    {
        var normalizedFilter = filter.Replace('\\', '/');
        var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        return relative.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
            || FileSystemName.MatchesSimpleExpression(normalizedFilter, relative, ignoreCase: true)
            || FileSystemName.MatchesSimpleExpression(normalizedFilter, Path.GetFileName(file), ignoreCase: true);
    }

    private static bool TryReadSpecification(
        string file,
        string repositoryRoot,
        List<string> skippedFiles,
        List<string> warnings,
        out SpecTraceSpecification specification)
    {
        specification = new SpecTraceSpecification(string.Empty, []);
        var relativeFile = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (JsonException ex)
        {
            skippedFiles.Add(relativeFile);
            warnings.Add($"{relativeFile}: invalid JSON: {ex.Message}");
            return false;
        }

        if (document is not JsonObject root)
        {
            skippedFiles.Add(relativeFile);
            warnings.Add($"{relativeFile}: not a JSON object.");
            return false;
        }

        if (!string.Equals(GetString(root, "artifact_type"), "specification", StringComparison.Ordinal))
        {
            skippedFiles.Add(relativeFile);
            warnings.Add($"{relativeFile}: not a supported JSON SpecTrace specification artifact.");
            return false;
        }

        var artifactId = GetString(root, "artifact_id");
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            skippedFiles.Add(relativeFile);
            warnings.Add($"{relativeFile}: missing artifact_id.");
            return false;
        }

        if (root["requirements"] is not JsonArray requirements)
        {
            skippedFiles.Add(relativeFile);
            warnings.Add($"{relativeFile}: missing requirements array.");
            return false;
        }

        specification = new SpecTraceSpecification(artifactId, requirements);
        return true;
    }

    private static IReadOnlyDictionary<string, SpecTraceArtifact> ReadArtifactIndex(string repositoryRoot)
    {
        var specsRoot = Path.Combine(repositoryRoot, "specs");
        if (!Directory.Exists(specsRoot))
        {
            return new Dictionary<string, SpecTraceArtifact>(StringComparer.Ordinal);
        }

        var artifacts = new Dictionary<string, SpecTraceArtifact>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(specsRoot, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root)
                {
                    continue;
                }

                var artifactId = GetString(root, "artifact_id");
                if (string.IsNullOrWhiteSpace(artifactId))
                {
                    continue;
                }

                artifacts[artifactId] = new SpecTraceArtifact(
                    artifactId,
                    GetString(root, "artifact_type"),
                    GetString(root, "status"));
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return artifacts;
    }

    private static RequirementEvidenceIndex ReadRequirementEvidenceIndex(string repositoryRoot)
    {
        var evidenceByRequirement = new Dictionary<string, RequirementEvidence>(StringComparer.Ordinal);
        foreach (var file in EnumerateEvidenceFiles(repositoryRoot))
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var relativeFile = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            foreach (Match match in RequirementIdRegex.Matches(content))
            {
                var requirementId = NormalizeRequirementId(match.Value);
                if (!evidenceByRequirement.TryGetValue(requirementId, out var evidence))
                {
                    evidence = new RequirementEvidence();
                    evidenceByRequirement[requirementId] = evidence;
                }

                if (IsTestEvidencePath(relativeFile))
                {
                    evidence.AddTestRef(relativeFile);
                }
                else if (IsSourceEvidencePath(relativeFile))
                {
                    evidence.AddSourceRef(relativeFile);
                }
                else
                {
                    evidence.AddTraceRef(relativeFile);
                }
            }
        }

        return new RequirementEvidenceIndex(evidenceByRequirement);
    }

    private static IEnumerable<string> EnumerateEvidenceFiles(string repositoryRoot)
    {
        var files = Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(file =>
            {
                var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                return !IsIgnoredEvidencePath(relative)
                    && IsTextEvidenceFile(file)
                    && (IsSourceEvidencePath(relative) || IsTestEvidencePath(relative));
            })
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static bool IsIgnoredEvidencePath(string relativePath)
    {
        return relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith(".vs/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextEvidenceFile(string file)
    {
        var extension = Path.GetExtension(file);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".fs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".vb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sh", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestEvidencePath(string relativePath)
    {
        return relativePath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains(".Tests/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains(".Test/", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(relativePath).Contains("Test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceEvidencePath(string relativePath)
    {
        return relativePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/src/", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<ClickUpRequirementRow> CreateRows(
        ClickUpRequirementExportRequest request,
        string repositoryRoot,
        string sourceFile,
        SpecTraceSpecification specification,
        List<string> warnings,
        IReadOnlyDictionary<string, SpecTraceArtifact> artifactIndex,
        RequirementEvidenceIndex evidenceIndex)
    {
        var relativeSourceFile = Path.GetRelativePath(repositoryRoot, sourceFile).Replace('\\', '/');
        var isRfcSpecification = IsRfcSpecification(specification, relativeSourceFile);
        var taskType = isRfcSpecification ? "Spec Req" : "Impl Req";
        foreach (var requirementNode in specification.Requirements)
        {
            if (requirementNode is not JsonObject requirement)
            {
                warnings.Add($"{relativeSourceFile}: skipped non-object requirement entry.");
                continue;
            }

            var id = GetString(requirement, "id");
            var title = GetString(requirement, "title");
            var statement = GetString(requirement, "statement");
            if (string.IsNullOrWhiteSpace(title))
            {
                warnings.Add($"{relativeSourceFile}: {DisplayId(id)} has no title.");
            }

            if (string.IsNullOrWhiteSpace(statement))
            {
                warnings.Add($"{relativeSourceFile}: {DisplayId(id)} has no statement.");
            }

            var trace = requirement["trace"] as JsonObject;
            var architectureIds = GetStringArray(trace, "satisfied_by");
            var workItemIds = GetStringArray(trace, "implemented_by");
            var verificationIds = GetStringArray(trace, "verified_by");
            var evidence = evidenceIndex.Get(id);
            var sourceInfo = GetSourceInfo(requirement, trace, architectureIds, workItemIds, verificationIds, evidence, isRfcSpecification);
            var taskName = $"{id} - {title}".Trim();
            var lifecycleState = GetLifecycleState(request, requirement, architectureIds, workItemIds, verificationIds, artifactIndex, evidence);

            yield return new ClickUpRequirementRow(
                taskName,
                taskType,
                "samuel@incursa.com",
                statement,
                id,
                request.Repository,
                specification.ArtifactId,
                $"https://github.com/{request.Repository}/blob/main/{relativeSourceFile}",
                lifecycleState.ImplementationState,
                lifecycleState.VerificationState,
                lifecycleState.TraceStatus,
                "Not collected",
                sourceInfo.SourceSection,
                sourceInfo.SourceRefs,
                request.SourceCommit,
                request.LastSync.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                JoinMultiline(architectureIds),
                JoinMultiline(workItemIds),
                JoinMultiline(verificationIds),
                relativeSourceFile,
                ComputeStableFingerprint(request.Repository, specification.ArtifactId, id, statement));
        }
    }

    private static LifecycleState GetLifecycleState(
        ClickUpRequirementExportRequest request,
        JsonObject requirement,
        IReadOnlyList<string> architectureIds,
        IReadOnlyList<string> workItemIds,
        IReadOnlyList<string> verificationIds,
        IReadOnlyDictionary<string, SpecTraceArtifact> artifactIndex,
        RequirementEvidence evidence)
    {
        if (request.SkipLifecycleStateDerivation)
        {
            return new LifecycleState(string.Empty, string.Empty, string.Empty);
        }

        var implementationState = GetImplementationState(requirement, workItemIds, artifactIndex, evidence);
        var verificationState = GetVerificationState(requirement, verificationIds, artifactIndex, evidence);
        var traceStatus = GetTraceStatus(architectureIds, workItemIds, verificationIds, implementationState, verificationState, evidence);
        return new LifecycleState(implementationState, verificationState, traceStatus);
    }

    private static bool IsRfcSpecification(SpecTraceSpecification specification, string relativeSourceFile)
    {
        return specification.ArtifactId.Contains("-RFC", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileNameWithoutExtension(relativeSourceFile).Contains("-RFC", StringComparison.OrdinalIgnoreCase);
    }

    private static SourceInfo GetSourceInfo(
        JsonObject requirement,
        JsonObject? trace,
        IReadOnlyList<string> architectureIds,
        IReadOnlyList<string> workItemIds,
        IReadOnlyList<string> verificationIds,
        RequirementEvidence evidence,
        bool isRfcSpecification)
    {
        var sourceSections = new List<string>();
        var sourceRefs = new List<string>();

        AddUnique(sourceSections, GetStringValues(requirement, "source_section", "sourceSection", "source_clause", "sourceClause", "source_location", "sourceLocation", "section", "clause"));
        AddUnique(sourceSections, GetNestedStringValues(requirement, "source", "section", "clause", "heading", "location"));
        AddUnique(sourceSections, GetNestedStringValues(requirement, "provenance", "section", "clause", "heading", "location"));

        if (isRfcSpecification)
        {
            AddRfcSourceInfo(requirement, trace, sourceSections, sourceRefs);
            return new SourceInfo(string.Join(" | ", sourceSections), string.Join(Environment.NewLine, sourceRefs));
        }

        foreach (var upstreamRef in GetStringValues(trace, "upstream_refs", "upstreamRefs"))
        {
            AddUnique(IsSourceSectionLike(upstreamRef) ? sourceSections : sourceRefs, upstreamRef);
        }

        AddUnique(sourceRefs, GetStringValues(requirement, "source_refs", "sourceRefs", "implementation_refs", "implementationRefs", "test_refs", "testRefs", "evidence_refs", "evidenceRefs", "code_refs", "codeRefs", "x_code_refs", "x_source_refs", "x_test_refs", "x_evidence_refs"));
        AddUnique(sourceRefs, GetStringValues(trace, "source_refs", "sourceRefs", "implementation_refs", "implementationRefs", "test_refs", "testRefs", "evidence_refs", "evidenceRefs", "code_refs", "codeRefs", "x_code_refs", "x_source_refs", "x_test_refs", "x_evidence_refs"));
        AddUnique(sourceRefs, architectureIds);
        AddUnique(sourceRefs, workItemIds);
        AddUnique(sourceRefs, verificationIds);
        AddUnique(sourceRefs, GetStringValues(trace, "related", "evidence", "evidence_refs", "evidenceRefs"));
        AddUnique(sourceRefs, evidence.SourceRefs);
        AddUnique(sourceRefs, evidence.TestRefs);
        AddUnique(sourceRefs, evidence.TraceRefs);

        return new SourceInfo(string.Join(" | ", sourceSections), string.Join(Environment.NewLine, sourceRefs));
    }

    private static void AddRfcSourceInfo(JsonObject requirement, JsonObject? trace, List<string> sourceSections, List<string> sourceRefs)
    {
        foreach (var upstreamRef in GetStringValues(trace, "upstream_refs", "upstreamRefs"))
        {
            if (IsUriLike(upstreamRef))
            {
                AddUnique(sourceRefs, upstreamRef);
            }
            else if (IsRfcSourceSectionLike(upstreamRef))
            {
                AddUnique(sourceSections, upstreamRef);
            }
        }

        foreach (var sourceRef in GetStringValues(requirement, "source_refs", "sourceRefs", "evidence_refs", "evidenceRefs"))
        {
            if (IsUriLike(sourceRef))
            {
                AddUnique(sourceRefs, sourceRef);
            }
            else if (IsRfcSourceSectionLike(sourceRef))
            {
                AddUnique(sourceSections, sourceRef);
            }
        }

        foreach (var sourceRef in GetStringValues(trace, "source_refs", "sourceRefs", "evidence_refs", "evidenceRefs"))
        {
            if (IsUriLike(sourceRef))
            {
                AddUnique(sourceRefs, sourceRef);
            }
            else if (IsRfcSourceSectionLike(sourceRef))
            {
                AddUnique(sourceSections, sourceRef);
            }
        }
    }

    private static bool IsSourceSectionLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPathLike(value) || IsUriLike(value))
        {
            return false;
        }

        return value.Contains('§', StringComparison.Ordinal)
            || value.Contains("section", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clause", StringComparison.OrdinalIgnoreCase)
            || value.Contains("heading", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("RFC ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("SPEC-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRfcSourceSectionLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPathLike(value) || IsUriLike(value))
        {
            return false;
        }

        return IsSourceSectionLike(value)
            || Regex.IsMatch(value, @"^RFC\s?\d{3,5}(?:[-\s].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static bool IsPathLike(string value)
    {
        return value.Contains('\\', StringComparison.Ordinal)
            || value.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("specs/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUriLike(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTraceStatus(
        IReadOnlyCollection<string> architectureIds,
        IReadOnlyCollection<string> workItemIds,
        IReadOnlyCollection<string> verificationIds,
        string implementationState,
        string verificationState,
        RequirementEvidence evidence)
    {
        var hasTrace = architectureIds.Count > 0
            || workItemIds.Count > 0
            || verificationIds.Count > 0
            || evidence.HasAnyEvidence;
        return hasTrace switch
        {
            false => "Unlinked",
            true when string.Equals(implementationState, "Implemented", StringComparison.Ordinal)
                && string.Equals(verificationState, "Passed", StringComparison.Ordinal) => "Auditable",
            _ => "Traceable",
        };
    }

    private static string GetImplementationState(
        JsonObject requirement,
        IReadOnlyCollection<string> workItemIds,
        IReadOnlyDictionary<string, SpecTraceArtifact> artifactIndex,
        RequirementEvidence evidence)
    {
        var explicitState = GetAllowedState(
            ImplementationStates,
            GetString(requirement, "implementation_state"),
            GetString(requirement, "implementationState"),
            GetString(requirement, "implementation_status"),
            GetString(requirement, "implementationStatus"));

        if (!string.IsNullOrWhiteSpace(explicitState))
        {
            return explicitState;
        }

        var statuses = GetStatuses(workItemIds, artifactIndex);
        if (statuses.Any(IsBlockedStatus))
        {
            return "Blocked";
        }

        if (statuses.Count > 0 && statuses.All(IsDeferredStatus))
        {
            return "Deferred";
        }

        if (statuses.Count > 0 && statuses.All(IsSupersededStatus))
        {
            return "Superseded";
        }

        if (statuses.Count > 0 && statuses.All(IsRetiredStatus))
        {
            return "Retired";
        }

        if (statuses.Count > 0 && statuses.All(IsImplementedStatus))
        {
            return "Implemented";
        }

        if (evidence.HasImplementationEvidence)
        {
            return "Implemented";
        }

        if (workItemIds.Count == 0)
        {
            return "Not started";
        }

        if (statuses.Count == 0)
        {
            return "Planned";
        }

        if (statuses.Any(IsInProgressStatus) || statuses.Any(IsImplementedStatus))
        {
            return "Partial";
        }

        return "Planned";
    }

    private static string GetVerificationState(
        JsonObject requirement,
        IReadOnlyCollection<string> verificationIds,
        IReadOnlyDictionary<string, SpecTraceArtifact> artifactIndex,
        RequirementEvidence evidence)
    {
        var explicitState = GetAllowedState(
            VerificationStates,
            GetString(requirement, "verification_state"),
            GetString(requirement, "verificationState"),
            GetString(requirement, "verification_status"),
            GetString(requirement, "verificationStatus"));

        if (!string.IsNullOrWhiteSpace(explicitState))
        {
            return explicitState;
        }

        var statuses = GetStatuses(verificationIds, artifactIndex);
        if (statuses.Any(IsFailedStatus))
        {
            return "Failed";
        }

        if (statuses.Any(IsBlockedStatus))
        {
            return "Blocked";
        }

        if (statuses.Count > 0 && statuses.All(IsWaivedStatus))
        {
            return "Waived";
        }

        if (statuses.Count > 0 && statuses.All(IsObsoleteStatus))
        {
            return "Obsolete";
        }

        if (statuses.Count > 0 && statuses.All(IsPassedStatus))
        {
            return "Passed";
        }

        if (evidence.HasVerificationEvidence)
        {
            return "Passed";
        }

        if (verificationIds.Count == 0)
        {
            return "None";
        }

        return "Planned";
    }

    private static IReadOnlyList<string> GetStatuses(
        IEnumerable<string> artifactIds,
        IReadOnlyDictionary<string, SpecTraceArtifact> artifactIndex)
    {
        return artifactIds
            .Select(id => artifactIndex.TryGetValue(id, out var artifact) ? artifact.Status : string.Empty)
            .Where(static status => !string.IsNullOrWhiteSpace(status))
            .ToList();
    }

    private static bool IsImplementedStatus(string status)
    {
        return IsAnyStatus(status, "complete", "completed", "done", "closed", "implemented");
    }

    private static bool IsInProgressStatus(string status)
    {
        return IsAnyStatus(status, "in_progress", "in-progress", "active", "partial", "started");
    }

    private static bool IsBlockedStatus(string status)
    {
        return IsAnyStatus(status, "blocked", "failed_blocked");
    }

    private static bool IsDeferredStatus(string status)
    {
        return IsAnyStatus(status, "deferred", "postponed");
    }

    private static bool IsSupersededStatus(string status)
    {
        return IsAnyStatus(status, "superseded", "replaced");
    }

    private static bool IsRetiredStatus(string status)
    {
        return IsAnyStatus(status, "retired", "removed", "deprecated");
    }

    private static bool IsPassedStatus(string status)
    {
        return IsAnyStatus(status, "passed", "verified", "complete", "completed");
    }

    private static bool IsFailedStatus(string status)
    {
        return IsAnyStatus(status, "failed", "failure");
    }

    private static bool IsWaivedStatus(string status)
    {
        return IsAnyStatus(status, "waived", "accepted_risk");
    }

    private static bool IsObsoleteStatus(string status)
    {
        return IsAnyStatus(status, "obsolete", "superseded", "retired", "deprecated");
    }

    private static bool IsAnyStatus(string status, params string[] expected)
    {
        var normalized = NormalizeState(status);
        return expected.Any(value => string.Equals(NormalizeState(value), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteCsv(string csvPath, IReadOnlyList<ClickUpRequirementRow> rows)
    {
        using var stream = File.Create(csvPath);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var csv = new CsvRowWriter(writer);

        csv.WriteRow(CsvHeaders);
        foreach (var row in rows)
        {
            csv.WriteRow(
            [
                row.TaskName,
                row.TaskType,
                row.AssignedTo,
                row.Description,
                row.SpecTraceId,
                row.Repository,
                row.SourceSpec,
                row.GitHubSource,
                row.ImplementationState,
                row.VerificationState,
                row.TraceStatus,
                row.EvidenceState,
                row.SourceSection,
                row.SourceRefs,
                row.SourceCommit,
                row.LastSync,
                row.ArchitectureIds,
                row.WorkItemIds,
                row.VerificationIds,
            ]);
        }
    }

    private static void WriteManifest(
        string manifestPath,
        ClickUpRequirementExportRequest request,
        string csvPath,
        IReadOnlyList<ClickUpRequirementRow> rows,
        IReadOnlyList<string> skippedFiles,
        IReadOnlyList<string> warnings)
    {
        var manifestRows = rows
            .Select((row, index) => new ClickUpRequirementExportManifestRow(
                row.SpecTraceId,
                row.SourceSpec,
                row.SourceFile,
                row.SourceCommit,
                row.TaskName,
                index + 2,
                row.StableFingerprint))
            .ToList();

        var manifest = new ClickUpRequirementExportManifest(
            request.Repository,
            request.SourceCommit,
            DateTimeOffset.Now,
            csvPath,
            rows.Count,
            skippedFiles,
            warnings,
            manifestRows);

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ComputeStableFingerprint(string repository, string sourceSpec, string requirementId, string statement)
    {
        var input = string.Join('\n', repository, sourceSpec, requirementId, statement);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? GetAllowedState(IReadOnlyList<string> allowedStates, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalizedCandidate = NormalizeState(candidate);
            var match = allowedStates.FirstOrDefault(
                allowedState => string.Equals(NormalizeState(allowedState), normalizedCandidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return null;
    }

    private static string NormalizeState(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizeRequirementId(string value)
    {
        return value.Replace('_', '-').ToUpperInvariant();
    }

    private static string GetString(JsonObject? obj, string propertyName)
    {
        return obj is not null && obj[propertyName] is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
    }

    private static IReadOnlyList<string> GetStringArray(JsonObject? obj, string propertyName)
    {
        if (obj is null || obj[propertyName] is not JsonArray values)
        {
            return [];
        }

        return values
            .Select(GetValueText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static IReadOnlyList<string> GetStringValues(JsonObject? obj, params string[] propertyNames)
    {
        if (obj is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var propertyName in propertyNames)
        {
            switch (obj[propertyName])
            {
                case JsonArray array:
                    AddUnique(values, array.Select(GetValueText));
                    break;
                case JsonValue value when value.TryGetValue<string>(out var stringValue):
                    AddUnique(values, stringValue);
                    break;
            }
        }

        return values;
    }

    private static IReadOnlyList<string> GetNestedStringValues(JsonObject obj, string objectName, params string[] propertyNames)
    {
        return obj[objectName] is JsonObject nested
            ? GetStringValues(nested, propertyNames)
            : [];
    }

    private static void AddUnique(List<string> values, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            AddUnique(values, candidate);
        }
    }

    private static void AddUnique(List<string> values, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || values.Contains(candidate, StringComparer.Ordinal))
        {
            return;
        }

        values.Add(candidate);
    }

    private static string GetValueText(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var stringValue)
            ? stringValue
            : node?.ToJsonString() ?? string.Empty;
    }

    private static string JoinMultiline(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, values);
    }

    private static string DisplayId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? "(missing id)" : id;
    }

    private static string GetRepositoryName(string repository)
    {
        var slashIndex = repository.LastIndexOf('/');
        return slashIndex >= 0 ? repository[(slashIndex + 1)..] : repository;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }

    private sealed record SpecTraceSpecification(string ArtifactId, JsonArray Requirements);

    private sealed record SpecTraceArtifact(string ArtifactId, string ArtifactType, string Status);

    private sealed record SourceInfo(string SourceSection, string SourceRefs);

    private sealed record LifecycleState(string ImplementationState, string VerificationState, string TraceStatus);

    private sealed class RequirementEvidence
    {
        public IReadOnlyList<string> SourceRefs => sourceRefs;

        public IReadOnlyList<string> TestRefs => testRefs;

        public IReadOnlyList<string> TraceRefs => traceRefs;

        public bool HasImplementationEvidence => sourceRefs.Count > 0;

        public bool HasVerificationEvidence => testRefs.Count > 0;

        public bool HasAnyEvidence => sourceRefs.Count > 0 || testRefs.Count > 0 || traceRefs.Count > 0;

        private readonly List<string> sourceRefs = [];

        private readonly List<string> testRefs = [];

        private readonly List<string> traceRefs = [];

        public void AddSourceRef(string value)
        {
            AddUnique(sourceRefs, value);
        }

        public void AddTestRef(string value)
        {
            AddUnique(testRefs, value);
        }

        public void AddTraceRef(string value)
        {
            AddUnique(traceRefs, value);
        }
    }

    private sealed class RequirementEvidenceIndex(Dictionary<string, RequirementEvidence> evidenceByRequirement)
    {
        public static readonly RequirementEvidenceIndex Empty = new(new Dictionary<string, RequirementEvidence>(StringComparer.Ordinal));

        private static readonly RequirementEvidence EmptyEvidence = new();

        public RequirementEvidence Get(string requirementId)
        {
            return evidenceByRequirement.TryGetValue(NormalizeRequirementId(requirementId), out var evidence)
                ? evidence
                : EmptyEvidence;
        }
    }
}
