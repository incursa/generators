namespace Incursa.Generators.AppDefinitions.Tests;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Input;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class ParserTests
{
    [Trait("Category", "Smoke")]
    [Fact]
    public void Parse_happy_path_definition_into_canonical_model()
    {
        var configLoader = new GeneratorConfigLoader();
        var diagnostics = new DiagnosticBag();
        var config = configLoader.Load(TestWorkspace.GetTestDataPath("HappyPath", "Source", "app-definitions.json"), diagnostics);

        config.ShouldNotBeNull();
        diagnostics.Items.ShouldBeEmpty();

        var parser = new AppDefinitionParser();
        var model = parser.Parse(config.DefinitionRoot, config.DefinitionPatterns, diagnostics);

        diagnostics.Items.ShouldBeEmpty();
        model.PageFeatures.Count.ShouldBe(1);

        var feature = model.PageFeatures.Single();
        feature.Name.ShouldBe("CustomerList");
        feature.RelativeDirectory.ShouldBe("Customer");
        feature.Route.ShouldBe("customers/{customerId}");
        feature.PageParameters.Select(static parameter => parameter.Name).ShouldBe(["customerId", "includeInactive"]);
        feature.ViewModelProperties.Select(static property => property.Name).ShouldBe(["Items", "Title"]);
        feature.OwnedTypes.Select(static type => type.Name).ShouldBe(["CustomerSummary"]);
        feature.ApiModels.Select(static type => type.Name).ShouldBe(["ArchiveCustomerRequest", "ArchiveResult"]);
        feature.Operations.Select(static operation => operation.Name).ShouldBe(["InitVm", "ArchiveCustomer"]);
    }
}
