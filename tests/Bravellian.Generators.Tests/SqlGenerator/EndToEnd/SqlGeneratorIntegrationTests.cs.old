using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator;

public class SqlGeneratorIntegrationTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void EndToEndGeneration_WithCompleteSchema_ShouldGenerateCorrectly()
    {
        // Arrange
        var sql = """
            CREATE TABLE [dbo].[Users] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [Name] nvarchar(255) NOT NULL,
                [Email] nvarchar(255) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [IsActive] bit NOT NULL DEFAULT(1),
                CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
                CONSTRAINT [UQ_Users_Email] UNIQUE ([Email])
            );
            
            CREATE TABLE [dbo].[Orders] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [UserId] int NOT NULL,
                [Total] decimal(18,2) NOT NULL,
                [OrderDate] datetime2 NOT NULL,
                CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
            );
            
            CREATE VIEW [dbo].[UserOrderSummary] AS
            SELECT 
                u.[Id] AS UserId,
                u.[Name] AS UserName,
                COUNT(o.[Id]) AS OrderCount,
                SUM(o.[Total]) AS TotalAmount
            FROM [dbo].[Users] u
            LEFT JOIN [dbo].[Orders] o ON u.[Id] = o.[UserId]
            GROUP BY u.[Id], u.[Name];
            
            CREATE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
            CREATE INDEX [IX_Orders_UserId] ON [dbo].[Orders] ([UserId]);
            """;

        var config = new SqlConfiguration
        {
            Namespace = "MyApp.Data.Entities",
            GlobalTypeMappings = new List<GlobalTypeMapping>
            {
                new GlobalTypeMapping 
                { 
                    Match = new GlobalTypeMappingMatch { SqlType = new List<string> { "nvarchar" } },
                    Apply = new GlobalTypeMappingApply { CSharpType = "string" }
                },
                new GlobalTypeMapping 
                {
                    Match = new GlobalTypeMappingMatch { SqlType = new List<string> { "datetime2" } },
                    Apply = new GlobalTypeMappingApply { CSharpType = "DateTime" }
                }
            },
            Tables = new Dictionary<string, TableConfiguration>
            {
                ["dbo.Users"] = new TableConfiguration
                {
                    CSharpClassName = "User",
                    ColumnOverrides = new Dictionary<string, ColumnOverride>
                    {
                        ["Id"] = new ColumnOverride { CSharpType = "int", IsNullable = false }
                    }
                },
                ["dbo.Orders"] = new TableConfiguration
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>
                    {
                        ["Total"] = new ColumnOverride { CSharpType = "decimal", IsNullable = false }
                    }
                }
            }
        };

        var ingestor = new SqlSchemaIngestor(_logger);
        var typeResolver = new SqlTypeResolver(_logger);

        // Apply type mapping overrides
        foreach (var typeMapping in config.GlobalTypeMappings)
        {
            foreach (var sqlType in typeMapping.Match.SqlType)
            {
                typeResolver.AddTypeMapping(sqlType, typeMapping.Apply.CSharpType);
            }
        }

        // Act
        var rawSchema = ingestor.IngestSqlSchema(sql);
        var schema = DatabaseSchema.FromRawSchema(rawSchema, config, _logger);

        // Assert
        Assert.NotNull(rawSchema);
        Assert.NotNull(schema);
        
        // Verify tables
        Assert.Equal(2, schema.Tables.Count);
        var userTable = schema.Tables.First(t => t.Name == "Users");
        var orderTable = schema.Tables.First(t => t.Name == "Orders");
        
        Assert.Equal("dbo", userTable.Schema);
        Assert.Equal(5, userTable.Columns.Count);
        
        Assert.Equal("dbo", orderTable.Schema);
        Assert.Equal(4, orderTable.Columns.Count);
        
        // Verify views
        Assert.Single(schema.Views);
        var view = schema.Views.First();
        Assert.Equal("UserOrderSummary", view.Name);
        Assert.Equal("dbo", view.Schema);
        Assert.Equal(4, view.Columns.Count);
        
        // Verify indexes
        Assert.Equal(2, schema.Indexes.Count);
        var emailIndex = schema.Indexes.First(i => i.Name == "IX_Users_Email");
        var userIdIndex = schema.Indexes.First(i => i.Name == "IX_Orders_UserId");
        
        Assert.Equal("Users", emailIndex.TableName);
        Assert.Single(emailIndex.Columns);
        Assert.Equal("Email", emailIndex.Columns.First());
        
        Assert.Equal("Orders", userIdIndex.TableName);
        Assert.Single(userIdIndex.Columns);
        Assert.Equal("UserId", userIdIndex.Columns.First());
        
        // Verify constraints
        Assert.Equal(4, schema.Constraints.Count);
        var primaryKeys = schema.Constraints.Where(c => c.Type == "PRIMARY KEY").ToList();
        var uniqueConstraints = schema.Constraints.Where(c => c.Type == "UNIQUE").ToList();
        var foreignKeys = schema.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();
        
        Assert.Equal(2, primaryKeys.Count);
        Assert.Single(uniqueConstraints);
        Assert.Single(foreignKeys);
        
        // Verify type resolution with overrides
        Assert.Equal("string", typeResolver.ResolveType("nvarchar", false, null, null, null));
        Assert.Equal("string?", typeResolver.ResolveType("nvarchar", true, null, null, null));
        Assert.Equal("DateTime", typeResolver.ResolveType("datetime2", false, null, null, null));
        Assert.Equal("DateTime?", typeResolver.ResolveType("datetime2", true, null, null, null));
        
        // Verify configuration overrides are available
        Assert.True(config.Tables.ContainsKey("dbo.Users"));
        var userTableConfig = config.Tables["dbo.Users"];
        Assert.Equal("User", userTableConfig.CSharpClassName);
        
        Assert.True(userTableConfig.ColumnOverrides.ContainsKey("Id"));
        var userIdOverride = userTableConfig.ColumnOverrides["Id"];
        Assert.Equal("int", userIdOverride.CSharpType);
        Assert.False(userIdOverride.IsNullable);
        
        Assert.True(config.Tables.ContainsKey("dbo.Orders"));
        var ordersTable = config.Tables["dbo.Orders"];
        Assert.True(ordersTable.ColumnOverrides.ContainsKey("Total"));
        var totalOverride = ordersTable.ColumnOverrides["Total"];
        Assert.Equal("decimal", totalOverride.CSharpType);
    }

    [Fact]
    public void EndToEndGeneration_WithMinimalSchema_ShouldGenerateCorrectly()
    {
        // Arrange
        var sql = """
            CREATE TABLE [dbo].[Test] (
                [Id] int NOT NULL,
                [Name] nvarchar(50) NOT NULL
            );
            """;

        var config = new SqlConfiguration
        {
            Namespace = "Test.Data"
        };

        var ingestor = new SqlSchemaIngestor(_logger);
        var typeResolver = new SqlTypeResolver(_logger);

        // Act
        var rawSchema = ingestor.IngestSqlSchema(sql);
        var schema = DatabaseSchema.FromRawSchema(rawSchema, config, _logger);

        // Assert
        Assert.NotNull(rawSchema);
        Assert.NotNull(schema);
        
        Assert.Single(schema.Tables);
        Assert.Empty(schema.Views);
        Assert.Empty(schema.Indexes);
        Assert.Empty(schema.Constraints);
        
        var table = schema.Tables.First();
        Assert.Equal("Test", table.Name);
        Assert.Equal(2, table.Columns.Count);
        
        var idColumn = table.Columns.First(c => c.Name == "Id");
        Assert.Equal("int", idColumn.DataType);
        Assert.False(idColumn.IsNullable);
        
        var nameColumn = table.Columns.First(c => c.Name == "Name");
        Assert.Equal("nvarchar", nameColumn.DataType);
        Assert.Equal(50, nameColumn.MaxLength);
        Assert.False(nameColumn.IsNullable);
        
        // Verify default type resolution
        Assert.Equal("int", typeResolver.ResolveType("int", false, null, null, null));
        Assert.Equal("string", typeResolver.ResolveType("nvarchar", false, 50, null, null));
    }

    [Fact]
    public void EndToEndGeneration_WithInvalidSql_ShouldHandleGracefully()
    {
        // Arrange
        var sql = """
            CREATE TABLE [Invalid (
                [Id] int NOT NULL,
                [Name] nvarchar(50) NOT NULL
            );
            """;

        var config = new SqlConfiguration
        {
            Namespace = "Test.Data"
        };

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act & Assert
        var rawSchema = ingestor.IngestSqlSchema(sql);
        var schema = DatabaseSchema.FromRawSchema(rawSchema, config, _logger);
        
        Assert.NotNull(rawSchema);
        Assert.NotNull(schema);
        
        // Should return empty schema for invalid SQL
        Assert.Empty(schema.Tables);
        Assert.Empty(schema.Views);
        Assert.Empty(schema.Indexes);
        Assert.Empty(schema.Constraints);
    }

    [Fact]
    public void EndToEndGeneration_WithComplexDataTypes_ShouldGenerateCorrectly()
    {
        // Arrange
        var sql = """
            CREATE TABLE [dbo].[ComplexTypes] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [DecimalValue] decimal(18,2) NOT NULL,
                [FloatValue] float(53) NOT NULL,
                [VarcharValue] varchar(max) NULL,
                [BinaryValue] varbinary(max) NULL,
                [DateTimeValue] datetime2(7) NOT NULL,
                [BitValue] bit NOT NULL,
                [GuidValue] uniqueidentifier NOT NULL,
                [TinyIntValue] tinyint NOT NULL,
                [SmallIntValue] smallint NOT NULL,
                [BigIntValue] bigint NOT NULL,
                [DateValue] date NOT NULL,
                [TimeValue] time NOT NULL,
                [DateTimeOffsetValue] datetimeoffset NOT NULL,
                CONSTRAINT [PK_ComplexTypes] PRIMARY KEY ([Id])
            );
            """;

        var config = new SqlConfiguration
        {
            Namespace = "MyApp.Data",
            GlobalTypeMappings = new List<GlobalTypeMapping>
            {
                new GlobalTypeMapping
                {
                    Match = new GlobalTypeMappingMatch { SqlType = new List<string> { "uniqueidentifier" } },
                    Apply = new GlobalTypeMappingApply { CSharpType = "Guid" }
                },
                new GlobalTypeMapping
                {
                    Match = new GlobalTypeMappingMatch { SqlType = new List<string> { "varbinary" } },
                    Apply = new GlobalTypeMappingApply { CSharpType = "byte[]" }
                }
            }
        };

        var ingestor = new SqlSchemaIngestor(_logger);
        var typeResolver = new SqlTypeResolver(_logger);

        // Apply type mapping overrides
        foreach (var typeMapping in config.GlobalTypeMappings)
        {
            foreach (var sqlType in typeMapping.Match.SqlType)
            {
                typeResolver.AddTypeMapping(sqlType, typeMapping.Apply.CSharpType);
            }
        }

        // Act
        var rawSchema = ingestor.IngestSqlSchema(sql);
        var schema = DatabaseSchema.FromRawSchema(rawSchema, config, _logger);

        // Assert
        Assert.NotNull(rawSchema);
        Assert.NotNull(schema);
        
        Assert.Single(schema.Tables);
        var table = schema.Tables.First();
        Assert.Equal("ComplexTypes", table.Name);
        Assert.Equal(14, table.Columns.Count);
        
        // Verify custom type mappings
        var guidColumn = table.Columns.First(c => c.Name == "GuidValue");
        Assert.Equal("uniqueidentifier", guidColumn.DataType);
        Assert.Equal("Guid", typeResolver.ResolveType(guidColumn.DataType, guidColumn.IsNullable, null, null, null));
        
        var binaryColumn = table.Columns.First(c => c.Name == "BinaryValue");
        Assert.Equal("varbinary", binaryColumn.DataType);
        Assert.Equal("byte[]?", typeResolver.ResolveType(binaryColumn.DataType, binaryColumn.IsNullable, null, null, null));
    }

    [Fact]
    public void EndToEndGeneration_WithJsonConfiguration_ShouldParseAndApplyCorrectly()
    {
        // Arrange
        var sql = """
            CREATE TABLE [dbo].[Products] (
                [Id] int PRIMARY KEY,
                [Name] nvarchar(100) NOT NULL,
                [Price] decimal(10, 2) NOT NULL,
                [CategoryId] int NULL
            );
            """;
            
        var jsonConfig = """
            {
                "namespace": "Store.Data",
                "generateNavigationProperties": true,
                "globalTypeMappings": [
                    {
                        "description": "Map decimal columns to Money type",
                        "priority": 100,
                        "match": {
                            "columnNameRegex": ".*Price$",
                            "sqlType": "decimal"
                        },
                        "apply": {
                            "csharpType": "Money"
                        }
                    }
                ],
                "tables": {
                    "dbo.Products": {
                        "description": "Product catalog items",
                        "csharpClassName": "Product",
                        "columnOverrides": {
                            "CategoryId": {
                                "description": "Foreign key to Categories table",
                                "csharpType": "CategoryId"
                            }
                        }
                    }
                }
            }
            """;
            
        var config = SqlConfiguration.FromJson(jsonConfig);
        var ingestor = new SqlSchemaIngestor(_logger);
        var typeResolver = new SqlTypeResolver(_logger);
        
        // Apply type mapping overrides
        foreach (var typeMapping in config.GlobalTypeMappings)
        {
            foreach (var sqlType in typeMapping.Match.SqlType)
            {
                typeResolver.AddTypeMapping(sqlType, typeMapping.Apply.CSharpType, 
                    columnNameRegex: typeMapping.Match.ColumnNameRegex);
            }
        }

        // Act
        var rawSchema = ingestor.IngestSqlSchema(sql);
        var schema = DatabaseSchema.FromRawSchema(rawSchema, config, _logger);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Store.Data", config.Namespace);
        Assert.True(config.GenerateNavigationProperties);
        
        // Verify global type mapping was parsed correctly
        Assert.Single(config.GlobalTypeMappings);
        var mapping = config.GlobalTypeMappings.First();
        Assert.Equal("Map decimal columns to Money type", mapping.Description);
        Assert.Equal(100, mapping.Priority);
        Assert.Equal(".*Price$", mapping.Match.ColumnNameRegex);
        Assert.Single(mapping.Match.SqlType);
        Assert.Equal("decimal", mapping.Match.SqlType.First());
        Assert.Equal("Money", mapping.Apply.CSharpType);
        
        // Verify table override was parsed correctly
        Assert.Single(config.Tables);
        Assert.True(config.Tables.ContainsKey("dbo.Products"));
        var productTable = config.Tables["dbo.Products"];
        Assert.Equal("Product catalog items", productTable.Description);
        Assert.Equal("Product", productTable.CSharpClassName);
        Assert.Single(productTable.ColumnOverrides);
        Assert.True(productTable.ColumnOverrides.ContainsKey("CategoryId"));
        var categoryIdOverride = productTable.ColumnOverrides["CategoryId"];
        Assert.Equal("Foreign key to Categories table", categoryIdOverride.Description);
        Assert.Equal("CategoryId", categoryIdOverride.CSharpType);
    }
}
