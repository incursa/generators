namespace Incursa.Generators.SpecTrace.Tests;

using System.Text;
using System.Text.Json;
using Incursa.Generators.SpecTrace.ClickUp;
using Incursa.Generators.Tool.Console;

public sealed class ClickUpRequirementExporterTests
{
    [Fact]
    public void Export_writes_csv_headers_quoted_fields_defaults_and_manifest()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath"), workspace.RootPath);
        var csvPath = Path.Combine(workspace.RootPath, "out", "sample-repo.csv");

        var result = new ClickUpRequirementExporter().Export(
            new ClickUpRequirementExportRequest(
                workspace.RootPath,
                "incursa/sample-repo",
                "abc123",
                csvPath,
                null,
                new DateOnly(2026, 6, 26)));

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors));
        result.TotalRequirements.ShouldBe(4);
        result.CsvPath.ShouldBe(csvPath);
        result.ManifestPath.ShouldBe(Path.Combine(workspace.RootPath, "out", "sample-repo.requirements-import-manifest.json"));

        var csv = File.ReadAllText(csvPath);
        csv.ShouldStartWith("Task Name,Task type,Assigned To,Description,SpecTrace ID,Repository,Source Spec,GitHub Source,Implementation State,Verification State,Trace Status,Evidence State,Source Section,Source Refs,Source Commit,Last Sync,Architecture IDs,Work Item IDs,Verification IDs");
        csv.ShouldContain("\"REQ-SAMPLE-0001 - Quoted, requirement\"");
        csv.ShouldContain("Impl Req,samuel@incursa.com,");
        csv.ShouldContain("The exporter MUST write \"\"quoted\"\" CSV fields, including commas.");
        csv.ShouldNotContain("Statement:");
        csv.ShouldNotContain("Trace Summary:");
        csv.ShouldContain("REQ-SAMPLE-0002 - Default states,Impl Req");
        csv.ShouldContain("Implemented,Passed,Auditable,Not collected,,\"src/DefaultRequirement.cs");
        csv.ShouldContain("REQ-SAMPLE-0004 - No trace,Impl Req");
        csv.ShouldContain("Implemented,Passed,Auditable");
        csv.ShouldNotContain(",Linked,");
        result.Warnings.ShouldContain("2 row(s) have blank Source Section but populated Source Refs.");

        var records = ReadCsv(csvPath);
        var headers = records[0];
        var specTraceIdIndex = ColumnIndex(headers, "SpecTrace ID");
        var firstRequirement = records.Single(row => string.Equals(row[specTraceIdIndex], "REQ-SAMPLE-0001", StringComparison.Ordinal));
        var secondRequirement = records.Single(row => string.Equals(row[specTraceIdIndex], "REQ-SAMPLE-0002", StringComparison.Ordinal));
        var fourthRequirement = records.Single(row => string.Equals(row[specTraceIdIndex], "REQ-SAMPLE-0004", StringComparison.Ordinal));
        var sourceSectionIndex = ColumnIndex(headers, "Source Section");
        var sourceRefsIndex = ColumnIndex(headers, "Source Refs");
        var taskTypeIndex = ColumnIndex(headers, "Task type");
        var implementationStateIndex = ColumnIndex(headers, "Implementation State");
        var verificationStateIndex = ColumnIndex(headers, "Verification State");
        var traceStatusIndex = ColumnIndex(headers, "Trace Status");
        var descriptionIndex = ColumnIndex(headers, "Description");
        var architectureIdsIndex = ColumnIndex(headers, "Architecture IDs");
        var workItemIdsIndex = ColumnIndex(headers, "Work Item IDs");
        var verificationIdsIndex = ColumnIndex(headers, "Verification IDs");
        sourceRefsIndex.ShouldBe(sourceSectionIndex + 1);
        firstRequirement[taskTypeIndex].ShouldBe("Impl Req");
        firstRequirement[descriptionIndex].ShouldBe("The exporter MUST write \"quoted\" CSV fields, including commas.");
        firstRequirement[sourceSectionIndex].ShouldBe("Section 1.1");
        firstRequirement[sourceSectionIndex].ShouldNotContain("src/");
        firstRequirement[sourceSectionIndex].ShouldNotContain("tests/");
        firstRequirement[sourceRefsIndex].ShouldBe(string.Join(
            Environment.NewLine,
            "src/SampleRequirement.cs",
            "tests/SampleRequirementTests.cs",
            "SampleRequirement.Handle",
            "ARC-SAMPLE-0001",
            "ARC-SAMPLE-0002",
            "WI-SAMPLE-0001",
            "VER-SAMPLE-0001"));
        firstRequirement[architectureIdsIndex].ShouldBe(string.Join(Environment.NewLine, "ARC-SAMPLE-0001", "ARC-SAMPLE-0002"));
        firstRequirement[workItemIdsIndex].ShouldBe("WI-SAMPLE-0001");
        firstRequirement[verificationIdsIndex].ShouldBe("VER-SAMPLE-0001");
        secondRequirement[sourceSectionIndex].ShouldBeEmpty();
        secondRequirement[taskTypeIndex].ShouldBe("Impl Req");
        secondRequirement[sourceRefsIndex].ShouldBe(string.Join(Environment.NewLine, "src/DefaultRequirement.cs", "tests/DefaultRequirementTests.cs"));
        secondRequirement[implementationStateIndex].ShouldBe("Implemented");
        secondRequirement[verificationStateIndex].ShouldBe("Passed");
        secondRequirement[traceStatusIndex].ShouldBe("Auditable");
        fourthRequirement[sourceSectionIndex].ShouldBeEmpty();
        fourthRequirement[taskTypeIndex].ShouldBe("Impl Req");
        fourthRequirement[sourceRefsIndex].ShouldBeEmpty();
        fourthRequirement[implementationStateIndex].ShouldBe("Not started");
        fourthRequirement[verificationStateIndex].ShouldBe("None");
        fourthRequirement[traceStatusIndex].ShouldBe("Unlinked");

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        var root = manifestDocument.RootElement;
        root.GetProperty("repository").GetString().ShouldBe("incursa/sample-repo");
        root.GetProperty("sourceCommit").GetString().ShouldBe("abc123");
        root.GetProperty("totalRequirements").GetInt32().ShouldBe(4);
        root.GetProperty("rows").GetArrayLength().ShouldBe(4);
        root.GetProperty("rows")[0].GetProperty("specTraceId").GetString().ShouldBe("REQ-SAMPLE-0001");
        root.GetProperty("rows")[0].GetProperty("rowNumber").GetInt32().ShouldBe(2);
        root.GetProperty("rows")[0].GetProperty("stableFingerprint").GetString()!.Length.ShouldBe(64);
    }

    [Fact]
    public void Export_fails_when_duplicate_requirement_ids_are_found()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("Duplicate"), workspace.RootPath);

        var result = new ClickUpRequirementExporter().Export(
            new ClickUpRequirementExportRequest(
                workspace.RootPath,
                "incursa/sample-repo",
                null,
                Path.Combine(workspace.RootPath, "out", "sample-repo.csv"),
                null,
                new DateOnly(2026, 6, 26)));

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain("Duplicate requirement IDs found: REQ-DUP-0001");
        File.Exists(result.CsvPath).ShouldBeFalse();
    }

    [Fact]
    public void Export_can_split_rfc_specifications_and_skip_lifecycle_state_derivation()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("RfcSplit"), workspace.RootPath);

        var rollupCsvPath = Path.Combine(workspace.RootPath, "out", "sample-repo.csv");
        var rollupResult = new ClickUpRequirementExporter().Export(
            new ClickUpRequirementExportRequest(
                workspace.RootPath,
                "incursa/sample-repo",
                "abc123",
                rollupCsvPath,
                null,
                new DateOnly(2026, 6, 26),
                "SPEC-*-RFC*.json"));

        rollupResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, rollupResult.Errors));
        rollupResult.TotalRequirements.ShouldBe(1);
        var rollupRecords = ReadCsv(rollupCsvPath);
        var rollupHeaders = rollupRecords[0];
        rollupRecords[1][ColumnIndex(rollupHeaders, "SpecTrace ID")].ShouldBe("REQ-SAMPLE-ROLLUP-0001");

        var rfcCsvPath = Path.Combine(workspace.RootPath, "out", "RFC9001.csv");
        var rfcResult = new ClickUpRequirementExporter().Export(
            new ClickUpRequirementExportRequest(
                workspace.RootPath,
                "incursa/sample-repo",
                "abc123",
                rfcCsvPath,
                "SPEC-SAMPLE-RFC9001.json",
                new DateOnly(2026, 6, 26),
                null,
                true));

        rfcResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, rfcResult.Errors));
        rfcResult.TotalRequirements.ShouldBe(1);
        var rfcRecords = ReadCsv(rfcCsvPath);
        var rfcHeaders = rfcRecords[0];
        var rfcRequirement = rfcRecords[1];
        rfcRequirement[ColumnIndex(rfcHeaders, "SpecTrace ID")].ShouldBe("REQ-SAMPLE-RFC9001-S1-0001");
        rfcRequirement[ColumnIndex(rfcHeaders, "Task type")].ShouldBe("Spec Req");
        rfcRequirement[ColumnIndex(rfcHeaders, "Implementation State")].ShouldBeEmpty();
        rfcRequirement[ColumnIndex(rfcHeaders, "Verification State")].ShouldBeEmpty();
        rfcRequirement[ColumnIndex(rfcHeaders, "Trace Status")].ShouldBeEmpty();
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Section")].ShouldBe("RFC 9001 §1 | RFC9001-S1-B1-P1-S1");
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Refs")].ShouldBe("https://www.rfc-editor.org/rfc/rfc9001.html#section-1");
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Refs")].ShouldNotContain("tests/");
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Refs")].ShouldNotContain("ARC-");
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Refs")].ShouldNotContain("WI-");
        rfcRequirement[ColumnIndex(rfcHeaders, "Source Refs")].ShouldNotContain("VER-");
    }

    [Fact]
    public async Task Cli_export_clickup_requirements_writes_summary()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath"), workspace.RootPath);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var csvPath = Path.Combine(workspace.RootPath, "out", "sample-repo.csv");

        var exitCode = await ToolCommandRunner.RunAsync(
            [
                "export-clickup-requirements",
                "--repo-root",
                workspace.RootPath,
                "--repository",
                "incursa/sample-repo",
                "--source-commit",
                "abc123",
                "--out",
                csvPath,
            ],
            stdout,
            stderr,
            TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0, stderr.ToString());
        stdout.ToString().ShouldContain("Exported 4 requirement(s).");
        stdout.ToString().ShouldContain(csvPath);
        File.Exists(csvPath).ShouldBeTrue();
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadCsv(string path)
    {
        var content = File.ReadAllText(path);
        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if ((character == '\r' || character == '\n') && !inQuotes)
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                fields.Add(field.ToString());
                field.Clear();
                records.Add(fields.ToList());
                fields.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(fields.ToList());
        }

        return records;
    }

    private static int ColumnIndex(IReadOnlyList<string> headers, string name)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index], name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Column '{name}' was not found.");
    }
}
