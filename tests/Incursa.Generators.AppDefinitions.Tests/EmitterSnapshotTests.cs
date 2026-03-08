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

    [Fact]
    public void Generate_write_without_initvm_emits_page_load_hook()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteFile(
            "app-definitions.json",
            """
            {
              "version": 1,
              "definitionRoot": "defs",
              "definitionPatterns": [ "*.page.xml" ],
              "targets": {
                "contracts": {
                  "kind": "page-contract-models",
                  "directory": "generated/contracts",
                  "namespace": "Example.Contracts.Pages"
                },
                "uiEngines": {
                  "kind": "page-ui-engine-interface",
                  "directory": "generated/ui-engines",
                  "namespace": "Example.Server.UiEngines",
                  "imports": {
                    "contracts": "Example.Contracts.Pages"
                  }
                },
                "pageModelBases": {
                  "kind": "page-model-base",
                  "directory": "generated/page-models",
                  "namespace": "Example.Web.Pages.Base",
                  "imports": {
                    "contracts": "Example.Contracts.Pages",
                    "uiEngines": "Example.Server.UiEngines"
                  }
                }
              }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "Orders", "OrderList.page.xml"),
            """
            <PageFeature name="OrderList" route="orders/{orderId}">
              <PageParameters>
                <Parameter name="orderId" type="string" source="route" required="true" />
              </PageParameters>
              <ViewModelProperties>
                <Property name="Title" type="string" required="true" />
              </ViewModelProperties>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Write);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));

        var generatedPath = Path.Combine(workspace.RootPath, "generated", "page-models", "Orders", "OrderListPageModelBase.g.cs");
        var generatedContent = File.ReadAllText(generatedPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        generatedContent.ShouldContain("public virtual async Task OnGetAsync(CancellationToken cancellationToken)");
        generatedContent.ShouldContain("ViewModel = await InitializeViewModelAsync(cancellationToken);");
        generatedContent.ShouldContain("protected virtual Task<OrderListViewModel?> InitializeViewModelAsync(CancellationToken cancellationToken)");
        generatedContent.Contains("UiEngine.InitVmAsync", StringComparison.Ordinal).ShouldBeFalse();
    }
}
