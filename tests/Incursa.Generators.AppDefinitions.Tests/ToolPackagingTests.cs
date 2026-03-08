namespace Incursa.Generators.AppDefinitions.Tests;

using System.Xml.Linq;
using Incursa.Generators.AppDefinitions.Tests.Fixtures;

public sealed class ToolPackagingTests
{
    [Fact]
    public void Tool_project_is_configured_as_a_packable_dotnet_tool()
    {
        var projectPath = TestWorkspace.GetRepositoryPath("src", "Incursa.Generators.Tool", "Incursa.Generators.Tool.csproj");
        var document = XDocument.Load(projectPath);
        var properties = document.Root!.Elements().Single(static element => string.Equals(element.Name.LocalName, "PropertyGroup", StringComparison.Ordinal));

        properties.Elements().Single(static element => string.Equals(element.Name.LocalName, "PackAsTool", StringComparison.Ordinal)).Value.ShouldBe("true");
        properties.Elements().Single(static element => string.Equals(element.Name.LocalName, "ToolCommandName", StringComparison.Ordinal)).Value.ShouldBe("incursa-appdefs");
        properties.Elements().Single(static element => string.Equals(element.Name.LocalName, "PackageId", StringComparison.Ordinal)).Value.ShouldBe("Incursa.Generators.Tool");
    }
}
