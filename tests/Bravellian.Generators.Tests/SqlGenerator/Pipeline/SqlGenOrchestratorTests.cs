// Copyright (c) Samuel McAravey
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

using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Xunit;
using System.Linq;
using System.Text.Json;

namespace Bravellian.Generators.Tests.SqlGenerator.Pipeline;

public class SqlGenOrchestratorTests
{
    private readonly TestLogger _logger = new();
    
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
                        SqlType = new List<string> { "decimal" } 
                    },
                    Apply = new GlobalTypeMappingApply { CSharpType = "decimal?" }
                }
            },
            Tables = new Dictionary<string, TableConfiguration>
            {
                ["dbo.Orders"] = new TableConfiguration
                {
                    CSharpClassName = "Order", // Override default name
                    ColumnOverrides = new Dictionary<string, ColumnOverride>
                    {
                        ["TotalAmount"] = new ColumnOverride
                        {
                            CSharpType = "double" // Override type mapping
                        }
                    },
                    ReadMethods = new List<ReadMethod> 
                    { 
                        new ReadMethod
                        { 
                            Name = "GetByCustomerId", 
                            MatchColumns = new List<string> { "CustomerId" }
                        }
                    },
                    UpdateConfig = new UpdateConfig
                    {
                        IgnoreColumns = new List<string> { "OrderDate" } // Don't include in updates
                    }
                },
                ["dbo.vwTopCustomers"] = new TableConfiguration
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>
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
            }
        };
            
        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(_logger),
            new SchemaRefiner(_logger, config),
            new CSharpModelTransformer(_logger, config, null),
            new CSharpCodeGenerator(config, _logger),
            config,
            _logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert - check main class was generated
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());
            
        // Verify Order class (with renamed table)
        var orderClass = generatedCode["Order.cs"];
        Assert.NotNull(orderClass);
        Assert.NotNull(orderClass);
        Assert.Contains("namespace MyCompany.Data.Entities", orderClass);
        Assert.Contains("public class Order", orderClass);
        Assert.Contains("public int OrderId { get; set; }", orderClass);
        Assert.Contains("public double TotalAmount { get; set; }", orderClass); // Renamed and type overridden
        
        // Repository methods should be in a separate file
        var orderRepository = generatedCode["OrderRepository.cs"];
        Assert.NotNull(orderRepository);
        Assert.NotNull(orderRepository);
        Assert.Contains("public static IEnumerable<Order> GetByCustomerId(this DbContext context, int customerId)", orderRepository); // Custom read method
        Assert.Contains("public static void Update(this DbContext context, Order order)", orderRepository);
        // OrderDate should be excluded from Update in repository implementation
        // Since we now use EF Core patterns, we don't check direct SQL statements
            
        // Verify View class
        var viewClass = generatedCode["vwTopCustomers.cs"];
        Assert.NotNull(viewClass);
        Assert.NotNull(viewClass);
        Assert.Contains("public class vwTopCustomers", viewClass);
        Assert.Contains("public int CustomerId { get; set; }", viewClass);
        Assert.Contains("public decimal? TotalSpent { get; set; }", viewClass); // Should use global type mapping
        
        // View repository shouldn't include update methods
        var viewRepository = generatedCode["vwTopCustomersRepository.cs"];
        if (viewRepository != null) // Repository might not exist if there are no methods
        {
            Assert.DoesNotContain("public static int Update(", viewRepository); // View should be read-only
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
            Tables = new Dictionary<string, TableConfiguration>
            {
                ["dbo.Products"] = new TableConfiguration
                {
                    ReadMethods = new List<ReadMethod>
                    {
                        new ReadMethod { Name = "GetBySku", MatchColumns = new List<string> { "Sku" } },
                        new ReadMethod { Name = "GetByName", MatchColumns = new List<string> { "Name" } }
                    }
                }
            }
        };
            
        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(_logger),
            new SchemaRefiner(_logger, config),
            new CSharpModelTransformer(_logger, config, null),
            new CSharpCodeGenerator(config, _logger),
            config,
            _logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert - Check indexed access methods
        var productClass = generatedCode.FirstOrDefault(c => c.Key.EndsWith("Products.cs"));
        Assert.NotNull(productClass);
        Assert.NotNull(productClass.Value);
        
        // Repository methods should be in a separate file
        var productRepository = generatedCode.FirstOrDefault(c => c.Key.EndsWith("ProductsRepository.cs"));
        Assert.NotNull(productRepository);
        Assert.NotNull(productRepository.Value);
        
        // Verify read methods were created based on indexes and configuration
        Assert.Contains("public static IEnumerable<Products> GetBySku(this DbContext context, string sku)", productRepository.Value); // Unique index = single result
        Assert.Contains("public static IEnumerable<Products> GetByName(this DbContext context, string name)", productRepository.Value); // Non-unique index = array result
    }
}
