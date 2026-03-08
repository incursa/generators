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

    [Fact]
    public void Validate_allows_local_owned_type_inheritance_when_base_name_sorts_after_derived_name()
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
            Path.Combine("defs", "Inheritance.page.xml"),
            """
            <PageFeature name="Inheritance">
              <ViewModelProperties>
                <Property name="Items" type="System.Collections.Generic.IReadOnlyList&lt;CommitmentLine&gt;" required="true" />
              </ViewModelProperties>
              <ViewModelOwnedType name="ContractLine">
                <Property name="Name" type="string" required="true" />
              </ViewModelOwnedType>
              <ViewModelOwnedType name="CommitmentLine" inherits="ContractLine">
                <Property name="CommitmentId" type="string" required="true" />
              </ViewModelOwnedType>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        result.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Code == "APPDEF026");
    }

    [Fact]
    public void Validate_modern_contracts_fail_for_required_nullable_and_unsupported_metadata()
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
                  "namespace": "Example.Contracts"
                }
              }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "ModernBroken.page.xml"),
            """
            <PageFeature name="ModernBroken">
              <ViewModelProperties>
                <Property name="Title" type="string" required="true" nullable="true" />
                <Property name="SearchTerm" type="string" regex=".+"/>
              </ViewModelProperties>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeFalse();
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF043" && diagnostic.Message.Contains("Title", StringComparison.Ordinal));
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF044" && diagnostic.Message.Contains("SearchTerm", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_reserved_cancellation_token_parameter_name_in_modern_signatures()
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
                "uiEngines": {
                  "kind": "page-ui-engine-interface",
                  "directory": "generated/ui-engines",
                  "namespace": "Example.UiEngines"
                }
              }
            }
            """);

        workspace.WriteFile(
            Path.Combine("defs", "Reserved.page.xml"),
            """
            <PageFeature name="Reserved">
              <Operations>
                <Operation name="Search">
                  <QueryParameter name="cancellationToken" type="string" required="true" />
                </Operation>
              </Operations>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeFalse();
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "APPDEF042" && diagnostic.Message.Contains("cancellationToken", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_allows_legacy_signatures_to_reuse_page_parameter_names()
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
            Path.Combine("defs", "LegacyCompatible.page.xml"),
            """
            <PageFeature name="LegacyCompatible">
              <PageParameters>
                <PageParameter name="id" type="string" required="true" />
              </PageParameters>
              <Operations>
                <Operation name="Reload" apiRouteSegment="items/{id}">
                  <RouteParameter name="id" type="string" required="true" />
                </Operation>
              </Operations>
            </PageFeature>
            """);

        var result = new AppDefinitionGenerator().Execute(new GenerationRequest(configPath), GenerationExecutionMode.Validate);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        result.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Code == "APPDEF023");
    }
}
