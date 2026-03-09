namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Pipeline;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class CheckModeTests
{
    [Trait("Category", "Smoke")]
    [Fact]
    public void Generate_check_detects_out_of_date_outputs()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath", "Source"));

        var configPath = Path.Combine(workspace.RootPath, "app-definitions.json");
        var generator = new AppDefinitionGenerator();
        var initialResult = generator.Execute(new GenerationRequest(configPath), GenerationExecutionMode.Write);

        initialResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, initialResult.Diagnostics));

        var generatedRoot = Path.Combine(workspace.RootPath, "generated");
        var outOfDateFile = Path.Combine(generatedRoot, "contracts", "Customer", "CustomerListContracts.g.cs");
        File.AppendAllText(outOfDateFile, "// stale mutation\n");

        var checkResult = generator.Execute(new GenerationRequest(configPath), GenerationExecutionMode.Check);

        checkResult.Success.ShouldBeFalse();
        checkResult.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF035");
    }

    [Fact]
    public void Generate_check_detects_orphaned_outputs_for_removed_definition()
    {
        using var workspace = new TestWorkspace();
        workspace.CopyDirectory(TestWorkspace.GetTestDataPath("HappyPath", "Source"));

        var configPath = Path.Combine(workspace.RootPath, "app-definitions.json");
        var generator = new AppDefinitionGenerator();
        var initialResult = generator.Execute(new GenerationRequest(configPath), GenerationExecutionMode.Write);

        initialResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, initialResult.Diagnostics));
        File.Delete(Path.Combine(workspace.RootPath, "defs", "Customer", "CustomerList.page.xml"));

        var checkResult = generator.Execute(new GenerationRequest(configPath), GenerationExecutionMode.Check);

        checkResult.Success.ShouldBeFalse();
        var orphanDiagnosticCodes = checkResult.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray();
        orphanDiagnosticCodes.ShouldContain("APPDEF036");
    }
}
