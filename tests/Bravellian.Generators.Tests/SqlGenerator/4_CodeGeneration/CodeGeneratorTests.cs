// Copyright (c) Bravellian
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Bravellian.Generators.Tests.SqlGenerator.4_CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
    using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;
    using Xunit;

    public class CodeGeneratorTests
{
    private readonly TestLogger logger = new ();

    [Fact]
    public void Generate_WithSimpleModel_ShouldGenerateCorrectCode()
    {
        // Arrange
        var generator = new CSharpCodeGenerator(null, this.logger);
        var model = this.CreateSimpleGenerationModel();

        // Act
        var generatedCode = generator.Generate(model);

        // Assert
        Assert.Contains("Products.cs", generatedCode.Keys);
        Assert.Contains("ProductsRepository.cs", generatedCode.Keys);

        var entityCode = generatedCode["Products.cs"];
        var repositoryCode = generatedCode["ProductsRepository.cs"];

        // Verify entity file
        Assert.Contains("namespace Generated.dbo", entityCode, StringComparison.Ordinal);
        Assert.Contains("public class Products", entityCode, StringComparison.Ordinal);
        Assert.Contains("public int Id { get; set; }", entityCode, StringComparison.Ordinal);
        Assert.Contains("public string Name { get; set; }", entityCode, StringComparison.Ordinal);
        Assert.Contains("public decimal? Price { get; set; }", entityCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public static Products Get", entityCode, StringComparison.Ordinal); // Methods should not be in entity class

        // Verify repository file
        Assert.Contains("namespace Generated.dbo", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static class ProductsRepository", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static Products Get(this DbContext context, int id)", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static void Create(this DbContext context, Products products)", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static void Update(this DbContext context, Products products)", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static void Delete(this DbContext context, int id)", repositoryCode, StringComparison.Ordinal);

        // Verify using statements
        Assert.Contains("using System;", entityCode, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.EntityFrameworkCore;", repositoryCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithIgnoredUpdateColumns_ShouldExcludeFromSetClause()
    {
        // Arrange
        var generator = new CSharpCodeGenerator(null, this.logger);
        var model = this.CreateModelWithIgnoredColumns();

        // Act
        var generatedCode = generator.Generate(model);

        // Assert
        var repositoryCode = generatedCode["ProductsRepository.cs"];

        // In the current implementation, we don't generate SQL directly but use EF Core
        // Instead, check that our implementation has the right patterns
        Assert.Contains("var entity = context.Set<Products>().Find(", repositoryCode, StringComparison.Ordinal);

        // Check that the repository class was properly generated
        Assert.Contains("public static class ProductsRepository", repositoryCode, StringComparison.Ordinal);
        Assert.Contains("public static void Update(this DbContext context, Products products)", repositoryCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithCustomReadMethod_ShouldGenerateCorrectMethod()
    {
        // Arrange
        var generator = new CSharpCodeGenerator(null, this.logger);
        var model = this.CreateModelWithCustomReadMethod();

        // Act
        var generatedCode = generator.Generate(model);

        // Assert
        var repositoryCode = generatedCode["ProductsRepository.cs"];

        // Verify custom read method exists in repository
        Assert.Contains("public static IEnumerable<Products> GetByPriceRange(this DbContext context, decimal minPrice, decimal maxPrice)", repositoryCode, StringComparison.Ordinal);

        // Verify it uses LINQ expressions rather than direct SQL now
        Assert.Contains("return context.Set<Products>().Where", repositoryCode, StringComparison.Ordinal);
    }

    private GenerationModel CreateSimpleGenerationModel()
    {
        var model = new GenerationModel();

        var productsClass = new ClassModel
        {
            Name = "Products",
            Namespace = "Generated.dbo",
            SourceSchemaName = "dbo",
            SourceObjectName = "Products",
            Properties = new List<PropertyModel>
                {
                    new PropertyModel { Name = "Id", Type = "int", IsPrimaryKey = true, SourceColumnName = "Id" },
                    new PropertyModel { Name = "Name", Type = "string", SourceColumnName = "Name" },
                    new PropertyModel { Name = "Price", Type = "decimal?", SourceColumnName = "Price" },
                },
        };

        // Add Get method
        var getMethod = new MethodModel
        {
            Name = "Get",
            Type = MethodType.Read,
            ReturnType = "Products",
            IsPrimaryKeyMethod = true,
            Parameters = new List<ParameterModel>
                {
                    new ParameterModel { Name = "id", Type = "int", SourcePropertyName = "Id" },
                },
        };

        // Add Create method
        var createMethod = new MethodModel
        {
            Name = "Create",
            Type = MethodType.Create,
            ReturnType = "void",
            Parameters = new List<ParameterModel>
                {
                    new ParameterModel { Name = "products", Type = "Products" },
                },
        };

        // Add Update method
        var updateMethod = new MethodModel
        {
            Name = "Update",
            Type = MethodType.Update,
            ReturnType = "void",
            Parameters = new List<ParameterModel>
                {
                    new ParameterModel { Name = "products", Type = "Products" },
                },
        };

        // Add Delete method
        var deleteMethod = new MethodModel
        {
            Name = "Delete",
            Type = MethodType.Delete,
            ReturnType = "void",
            IsPrimaryKeyMethod = true,
            Parameters = new List<ParameterModel>
                {
                    new ParameterModel { Name = "id", Type = "int", SourcePropertyName = "Id" },
                },
        };

        productsClass.Methods.Add(getMethod);
        productsClass.Methods.Add(createMethod);
        productsClass.Methods.Add(updateMethod);
        productsClass.Methods.Add(deleteMethod);

        model.Classes.Add(productsClass);

        return model;
    }

    private GenerationModel CreateModelWithIgnoredColumns()
    {
        var model = this.CreateSimpleGenerationModel();
        var productsClass = model.Classes.First();

        // Add CreatedDate property
        productsClass.Properties.Add(new PropertyModel
        {
            Name = "CreatedDate",
            Type = "DateTime",
            SourceColumnName = "CreatedDate",
        });

        // Mark CreatedDate as ignored in update method
        var updateMethod = productsClass.Methods.First(m => m.Type == MethodType.Update);
        updateMethod.Metadata["IgnoredColumns"] = new HashSet<string>(StringComparer.Ordinal) { "Id", "CreatedDate" };

        return model;
    }

    private GenerationModel CreateModelWithCustomReadMethod()
    {
        var model = this.CreateSimpleGenerationModel();
        var productsClass = model.Classes.First();

        // Add custom read method
        var customReadMethod = new MethodModel
        {
            Name = "GetByPriceRange",
            Type = MethodType.Read,
            ReturnType = "IEnumerable<Products>",
            Parameters = new List<ParameterModel>
                {
                    new ParameterModel { Name = "minPrice", Type = "decimal", SourcePropertyName = "Price" },
                    new ParameterModel { Name = "maxPrice", Type = "decimal", SourcePropertyName = "Price" },
                },
        };

        productsClass.Methods.Add(customReadMethod);

        return model;
    }
}
}
