namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Pipeline;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class EmitterSnapshotTests
{
    [Trait("Category", "Smoke")]
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

    [Fact]
    public void Generate_write_modern_targets_support_feature_namespaces_base_type_route_helpers_and_task_like_returns()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteFile(
            "app-definitions.json",
            """
            {
              "version": 1,
              "definitionRoot": "defs",
              "definitionPatterns": [ "*.page.xml" ],
              "validation": {
                "knownTypeNames": [ "OrganizationIdentifier", "BillingPeriodIdentifier" ]
              },
              "targets": {
                "contracts": {
                  "kind": "page-contract-models",
                  "directory": "generated/contracts",
                  "namespace": "Example.Contracts.Pages",
                  "namespaceMode": "feature",
                  "appendRelativePathToNamespace": true
                },
                "uiEngines": {
                  "kind": "page-ui-engine-interface",
                  "directory": "generated/ui-engines",
                  "namespace": "Example.Server.UiEngines",
                  "namespaceMode": "feature",
                  "appendRelativePathToNamespace": true,
                  "imports": {
                    "contracts": "Example.Contracts.Pages"
                  }
                },
                "pageModelBases": {
                  "kind": "page-model-base",
                  "directory": "generated/page-models",
                  "namespace": "Example.Web.Pages.Base",
                  "namespaceMode": "feature",
                  "appendRelativePathToNamespace": true,
                  "baseType": "Example.Web.Infrastructure.FeaturePageModel",
                  "imports": {
                    "contracts": "Example.Contracts.Pages",
                    "uiEngines": "Example.Server.UiEngines.Organizations"
                  }
                },
                "pageRoutes": {
                  "kind": "page-route-helper",
                  "directory": "generated/routes",
                  "namespace": "Example.Web.Generated"
                }
              }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "Organizations", "OrganizationSettings.page.xml"),
            """
            <PageFeature name="OrganizationSettings" route="orgs/{organizationId}/settings">
              <PageParameters>
                <Parameter name="organizationId" type="OrganizationIdentifier" source="route" required="true" />
                <Parameter name="tab" type="string" source="query" required="false" />
              </PageParameters>
              <ViewModelProperties>
                <Property name="Title" type="string" required="true" />
                <Property name="MaskedConnectionString" type="string" nullable="true" required="false" />
              </ViewModelProperties>
              <OwnedTypes>
                <Type name="BillingMetrics">
                  <Property name="Count" type="int" required="true" />
                </Type>
              </OwnedTypes>
              <ApiModels>
                <Type name="SaveConnectionRequest">
                  <Property name="ConnectionString" type="string" required="true" />
                </Type>
              </ApiModels>
              <Operations>
                <Operation name="InitVm" returnType="System.Threading.Tasks.Task&lt;OrganizationSettingsViewModel&gt;" />
                <Operation name="SaveConnection" returnType="System.Threading.Tasks.Task">
                  <BodyParameter type="SaveConnectionRequest" />
                </Operation>
              </Operations>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Write);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));

        var contracts = File.ReadAllText(Path.Combine(workspace.RootPath, "generated", "contracts", "Organizations", "OrganizationSettingsContracts.g.cs"));
        contracts.ShouldContain("namespace Example.Contracts.Pages.Organizations.OrganizationSettings;");
        contracts.ShouldContain("public partial class OrganizationSettingsViewModel");
        contracts.ShouldContain("public required string Title { get; init; }");
        contracts.ShouldContain("public string? MaskedConnectionString { get; init; }");

        var uiEngine = File.ReadAllText(Path.Combine(workspace.RootPath, "generated", "ui-engines", "Organizations", "IOrganizationSettingsUiEngine.g.cs"));
        uiEngine.ShouldContain("using Example.Contracts.Pages.Organizations.OrganizationSettings;");
        uiEngine.ShouldContain("Task<OrganizationSettingsViewModel> InitVmAsync(OrganizationIdentifier organizationId, string? tab, CancellationToken cancellationToken);");
        uiEngine.ShouldContain("Task SaveConnectionAsync(OrganizationIdentifier organizationId, string? tab, SaveConnectionRequest body, CancellationToken cancellationToken);");
        uiEngine.ShouldNotContain("Task<System.Threading.Tasks.Task>");

        var pageModelBase = File.ReadAllText(Path.Combine(workspace.RootPath, "generated", "page-models", "Organizations", "OrganizationSettingsPageModelBase.g.cs"));
        pageModelBase.ShouldContain("using Example.Contracts.Pages.Organizations.OrganizationSettings;");
        pageModelBase.ShouldContain("public abstract partial class OrganizationSettingsPageModelBase : Example.Web.Infrastructure.FeaturePageModel");
        pageModelBase.ShouldContain("public string? Tab { get; set; }");
        pageModelBase.ShouldContain("ViewModel = await UiEngine.InitVmAsync(OrganizationId, Tab, cancellationToken);");

        var routes = File.ReadAllText(Path.Combine(workspace.RootPath, "generated", "routes", "PageRoutes.g.cs"));
        routes.ShouldContain("public const string OrganizationSettingsRoute = \"/orgs/{organizationId}/settings\";");
        routes.ShouldContain("public static string GetOrganizationSettingsPath(OrganizationIdentifier organizationId, string? tab)");
        routes.ShouldContain("if (tab is not null)");
    }
}
