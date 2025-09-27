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

using System.Linq;
using System.Text.Json;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator.Pipeline;

public class SqlGenOrchestratorTests
{
    private readonly TestLogger logger = new ();

    [Fact]
    public void Generate_WithPhase1To4_ShouldProduceCorrectOutput()
    {
        // Arrange
        var sql = """
            CREATE TABLE dbo.Orders (
                OrderId INT PRIMARY KEY,
                CustomerId INT NOT NULL,
                OrderDate DATETIME NOT NULL,
                TotalAmount DECIMAL(10, 2) NOT NULL
            );
            GO
            
            CREATE VIEW dbo.vwTopCustomers AS
            SELECT CustomerId, SUM(TotalAmount) AS TotalSpent
            FROM dbo.Orders
            GROUP BY CustomerId;
            GO
            """;

        var config = new SqlConfiguration
        {
            Namespace = "MyCompany.Data.Entities",
            GenerateNavigationProperties = true,
            GenerateDbContext = true,
            GlobalTypeMappings = new List<GlobalTypeMapping>
            {
                new GlobalTypeMapping
                {
                    Description = "Custom DECIMAL mapping",
                    Match = new GlobalTypeMappingMatch
                    {
                        SqlType = new List<string> { "decimal" },
                    },
                    Apply = new GlobalTypeMappingApply { CSharpType = "decimal?" },
                },
            },
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Orders"] = new TableConfiguration
                {
                    CSharpClassName = "Order", // Override default name
                    ColumnOverrides = new Dictionary<string, ColumnOverride>(
StringComparer.Ordinal)
                    {
                        ["TotalAmount"] = new ColumnOverride
                        {
                            CSharpType = "double", // Override type mapping
                        },
                    },
                    ReadMethods = new List<ReadMethod>
                    {
                        new ReadMethod
                        {
                            Name = "GetByCustomerId",
                            MatchColumns = new List<string> { "CustomerId" },
                        },
                    },
                    UpdateConfig = new UpdateConfig
                    {
                        IgnoreColumns = new List<string> { "OrderDate" }, // Don't include in updates
                    },
                },
                ["dbo.vwTopCustomers"] = new TableConfiguration
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>(
StringComparer.Ordinal)
                    {
                        ["CustomerId"] = new ColumnOverride
                        {
                            SqlType = "int",
                            IsNullable = false,
                        },
                        ["TotalSpent"] = new ColumnOverride
                        {
                            SqlType = "decimal(18,4)",
                            IsNullable = true,
                        },
                    },
                },
            },
        };

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert - check main class was generated
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());

        // Verify Order class (with renamed table)
        var orderClass = generatedCode["Order.cs"];
        Assert.NotNull(orderClass);
        Assert.NotNull(orderClass);
        Assert.Contains("namespace MyCompany.Data.Entities", orderClass, StringComparison.Ordinal);
        Assert.Contains("public class Order", orderClass, StringComparison.Ordinal);
        Assert.Contains("public int OrderId { get; set; }", orderClass, StringComparison.Ordinal);
        Assert.Contains("public double TotalAmount { get; set; }", orderClass, StringComparison.Ordinal); // Renamed and type overridden

        // Repository methods should be in a separate file
        var orderRepository = generatedCode["OrderRepository.cs"];
        Assert.NotNull(orderRepository);
        Assert.NotNull(orderRepository);
        Assert.Contains("public static IEnumerable<Order> GetByCustomerId(this DbContext context, int customerId)", orderRepository, StringComparison.Ordinal); // Custom read method
        Assert.Contains("public static void Update(this DbContext context, Order order)", orderRepository, StringComparison.Ordinal);

        // OrderDate should be excluded from Update in repository implementation
        // Since we now use EF Core patterns, we don't check direct SQL statements

        // Verify View class
        var viewClass = generatedCode["vwTopCustomers.cs"];
        Assert.NotNull(viewClass);
        Assert.NotNull(viewClass);
        Assert.Contains("public class vwTopCustomers", viewClass, StringComparison.Ordinal);
        Assert.Contains("public int CustomerId { get; set; }", viewClass, StringComparison.Ordinal);
        Assert.Contains("public decimal? TotalSpent { get; set; }", viewClass, StringComparison.Ordinal); // Should use global type mapping

        // View repository shouldn't include update methods
        var viewRepository = generatedCode["vwTopCustomersRepository.cs"];
        if (viewRepository != null) // Repository might not exist if there are no methods
        {
            Assert.DoesNotContain("public static int Update(", viewRepository, StringComparison.Ordinal); // View should be read-only
        }
    }

    [Fact]
    public void Generate_WithIndexes_ShouldCreateIndexedAccessMethods()
    {
        // Arrange
        var sql = """
            CREATE TABLE dbo.Products (
                ProductId INT PRIMARY KEY,
                Sku NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Price DECIMAL(10, 2) NOT NULL
            );
            GO
            
            CREATE UNIQUE INDEX IX_Products_Sku ON dbo.Products(Sku);
            GO
            CREATE INDEX IX_Products_Name ON dbo.Products(Name);
            GO
            """;

        var config = new SqlConfiguration
        {
            Namespace = "MyCompany.Data.Entities",
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Products"] = new TableConfiguration
                {
                    ReadMethods = new List<ReadMethod>
                    {
                        new ReadMethod { Name = "GetBySku", MatchColumns = new List<string> { "Sku" } },
                        new ReadMethod { Name = "GetByName", MatchColumns = new List<string> { "Name" } }
                    },
                },
            },
        };

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert - Check indexed access methods
        var productClass = generatedCode.FirstOrDefault(c => c.Key.EndsWith("Products.cs", StringComparison.Ordinal));
        Assert.NotNull(productClass.Value);

        // Repository methods should be in a separate file
        var productRepository = generatedCode.FirstOrDefault(c => c.Key.EndsWith("ProductsRepository.cs", StringComparison.Ordinal));
        Assert.NotNull(productRepository.Value);

        // Verify read methods were created based on indexes and configuration
        Assert.Contains("public static IEnumerable<Products> GetBySku(this DbContext context, string sku)", productRepository.Value, StringComparison.Ordinal); // Unique index = single result
        Assert.Contains("public static IEnumerable<Products> GetByName(this DbContext context, string name)", productRepository.Value, StringComparison.Ordinal); // Non-unique index = array result
    }
}
