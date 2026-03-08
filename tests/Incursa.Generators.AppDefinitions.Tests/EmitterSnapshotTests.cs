namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Pipeline;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class EmitterSnapshotTests
{
    [Fact]
    public void Generate_write_matches_checked_in_golden_files()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath", "Source"));

        var configPath = Path.Combine(workspace.RootPath, "app-definitions.json");
        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Write);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        result.GeneratedFiles.Count.ShouldBe(4);

        var expected = TestWorkspace.ReadFiles(TestWorkspace.GetTestDataPath("HappyPath", "Expected"));
        var actual = TestWorkspace.ReadFiles(Path.Combine(workspace.RootPath, "generated"));

        actual.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ShouldBe(expected.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase));
        foreach (var pair in expected)
        {
            actual[pair.Key].ShouldBe(pair.Value, customMessage: $"Mismatch for generated file '{pair.Key}'.");
        }
    }
}
