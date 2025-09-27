// Fix namespace imports
using Xunit;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
using Xunit;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;

namespace Bravellian.Generators.Tests.SqlGenerator._4_CodeGeneration;

public class CodeGenerationTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void Generate_EntityClass_ShouldProduceCorrectCode()
    {
        // Arrange
        var csharpModel = new GenerationModel
        {
            Classes =
            [
                new() {
                    Name = "User",
                    Properties =
                    [
                        new() { Name = "Id", Type = "int" },
                        new() { Name = "Name", Type = "string" }
                    ],
                    SourceObjectName = "User",
                    SourceSchemaName = "dbo",
                    IsView = false,
                }
            ]
        };
        var generator = new CSharpCodeGenerator(null, _logger);

        // Act
        var code = generator.Generate(csharpModel)["User.cs"];

        // Assert
        Assert.Contains("public class User", code);
        Assert.Contains("public int Id { get; set; }", code);
        Assert.Contains("public string Name { get; set; }", code);
    }

    [Fact]
    public void Generate_WithNamespace_ShouldUseCorrectNamespaceAndOrganizeFiles()
    {
        // Arrange
        var model = new GenerationModel
        {
            Classes =
            [
                new() {
                    Name = "Customer",
                    Namespace = "MyProject.Data.Sales",
                    SourceSchemaName = "sales",
                    SourceObjectName = "Customer",
                    IsView = false,
                    Properties = [ new() { Name = "Id", Type = "int", IsPrimaryKey = true } ],
                    Methods = [ new() { Name = "GetById", Type = MethodType.Read, ReturnType = "Customer" } ],
                    CreateInput = new() { Name = "CustomerCreateInput", Namespace = "MyProject.Data.Sales" },
                }
            ]
        };
        var generator = new CSharpCodeGenerator(null, _logger);

        // Act
        var files = generator.Generate(model);

        // Assert
        Assert.Contains("sales\\Customer.g.cs", files.Keys);
        Assert.Contains("sales\\CustomerRepository.g.cs", files.Keys);
        Assert.Contains("sales\\CustomerCreateInput.g.cs", files.Keys);

        Assert.Contains("namespace MyProject.Data.Sales;", files["sales\\Customer.g.cs"]);
        Assert.Contains("namespace MyProject.Data.Sales;", files["sales\\CustomerRepository.g.cs"]);
        Assert.Contains("namespace MyProject.Data.Sales;", files["sales\\CustomerCreateInput.g.cs"]);
    }

    [Fact]
    public void Generate_ForView_ShouldOnlyGenerateEntityClass()
    {
        // Arrange
        var model = new GenerationModel
        {
            Classes =
            [
                new() {
                    Name = "CustomerOrdersView",
                    Namespace = "MyProject.Data.Dbo",
                    SourceSchemaName = "dbo",
                    SourceObjectName = "CustomerOrdersView",
                    IsView = true, // This is a view
                    Properties =
                    [
                        new() { Name = "CustomerName", Type = "string" },
                        new() { Name = "OrderTotal", Type = "decimal" }
                    ],
                    Methods = [], // Views shouldn't have data modification methods
                }
            ]
        };
        var generator = new CSharpCodeGenerator(null, _logger);

        // Act
        var files = generator.Generate(model);

        // Assert
        Assert.Contains("dbo\\CustomerOrdersView.g.cs", files.Keys);
        Assert.DoesNotContain("CustomerOrdersViewRepository.g.cs", files.Keys);
        Assert.DoesNotContain("CustomerOrdersViewCreateInput.g.cs", files.Keys);

        Assert.Contains("namespace MyProject.Data.Dbo;", files["dbo\\CustomerOrdersView.g.cs"]);
    }
}

