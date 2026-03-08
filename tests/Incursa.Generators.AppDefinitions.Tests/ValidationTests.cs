namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Pipeline;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class ValidationTests
{
    [Fact]
    public void Validate_reports_duplicates_unresolved_types_and_route_mismatches()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteFile(
            "app-definitions.json",
            """
            {
              "version": 1,
              "definitionRoot": "defs",
              "definitionPatterns": [ "*.page.xml" ],
              "targets": { }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "Broken.page.xml"),
            """
            <PageFeature name="BrokenFeature">
              <ViewModelProperties>
                <Property name="Items" type="UnknownType" required="true" />
              </ViewModelProperties>
              <Operations>
                <Operation name="Save" />
                <Operation name="Save" />
                <Operation name="RouteMismatch" apiRouteSegment="items/{id}">
                  <RouteParameter name="otherId" type="string" required="true" />
                </Operation>
              </Operations>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeFalse();
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF026" && diagnostic.Message.Contains("UnknownType", StringComparison.Ordinal));
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF028" && diagnostic.Message.Contains("Save", StringComparison.Ordinal));
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF029" && diagnostic.Message.Contains("otherId", StringComparison.Ordinal));
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF030" && diagnostic.Message.Contains("id", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_warns_that_missing_initvm_uses_initialize_hook()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteFile(
            "app-definitions.json",
            """
            {
              "version": 1,
              "definitionRoot": "defs",
              "definitionPatterns": [ "*.page.xml" ],
              "targets": { }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "NoInit.page.xml"),
            """
            <PageFeature name="NoInit">
              <ViewModelProperties>
                <Property name="Title" type="string" required="true" />
              </ViewModelProperties>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF022"
            && diagnostic.Message.Contains("InitializeViewModelAsync", StringComparison.Ordinal));
    }
}
