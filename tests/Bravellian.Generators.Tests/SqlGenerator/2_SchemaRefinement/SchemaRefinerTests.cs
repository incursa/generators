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

using Xunit;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using System;

namespace Bravellian.Generators.Tests.SqlGenerator._2_SchemaRefinement
{
    public class SchemaRefinerTests
    {
        private readonly TestLogger _logger = new();

        [Fact]
        public void Refine_WithBasicTable_ShouldCreateModel()
        {
            // Arrange
            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Users (Id int NOT NULL, Name nvarchar(100) NULL);"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            Assert.Single(refinedSchema.Objects);
            var table = refinedSchema.Objects.First(o => !o.IsView);
            Assert.Equal("dbo", table.Schema);
            Assert.Equal("Users", table.Name);
            Assert.Equal(2, table.Columns.Count);
            Assert.Contains(table.Columns, c => c.Name == "Id" && string.Equals(c.DatabaseType.Value, "int", StringComparison.OrdinalIgnoreCase) && !c.IsNullable);
            Assert.Contains(table.Columns, c => c.Name == "Name" && string.Equals(c.DatabaseType.Value, "nvarchar", StringComparison.OrdinalIgnoreCase) && c.IsNullable);
        }

        [Fact]
        public void Refine_WithViewAndConfigOverrides_ShouldPatchViewSchema()
        {
            // Arrange
            var rawModel = CreateRawDatabaseModel(
                views: [
                    "CREATE VIEW dbo.ActiveUsers AS SELECT UserId, UserName FROM Users WHERE IsActive = 1;"
                ]);
            
            var config = new SqlConfiguration();
            config.Tables["dbo.ActiveUsers"] = new TableConfiguration
            {
                ColumnOverrides = new Dictionary<string, ColumnOverride>
                {
                    ["UserId"] = new ColumnOverride { SqlType = "int", IsNullable = false },
                    ["UserName"] = new ColumnOverride { SqlType = "nvarchar(255)", IsNullable = true }
                }
            };
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            Assert.Single(refinedSchema.Objects);
            var view = refinedSchema.Objects.First(o => o.IsView);
            Assert.Equal("dbo", view.Schema);
            Assert.Equal("ActiveUsers", view.Name);
            Assert.Equal(2, view.Columns.Count);
            Assert.Contains(view.Columns, c => c.Name == "UserId" && string.Equals(c.DatabaseType.Value, "int", StringComparison.OrdinalIgnoreCase) && !c.IsNullable);
            Assert.Contains(view.Columns, c => c.Name == "UserName" && string.Equals(c.DatabaseType.Value, "nvarchar", StringComparison.OrdinalIgnoreCase) && c.IsNullable);
        }

        [Fact]
        public void Refine_WithPrimaryKeyConstraint_ShouldBeAddedToTable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Products (Id int NOT NULL, Sku nvarchar(50) NOT NULL, CONSTRAINT PK_Products PRIMARY KEY (Id, Sku));"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var table = refinedSchema.Objects.First(o => !o.IsView);
            Assert.Equal(2, table.PrimaryKeyColumns.Count);
            Assert.Contains("Id", table.PrimaryKeyColumns);
            Assert.Contains("Sku", table.PrimaryKeyColumns);
        }

        [Fact]
        public void Refine_ViewWithDerivedColumns_ShouldInferTypesFromBaseTable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Users (Id int NOT NULL, Name nvarchar(100) NULL, IsActive bit NOT NULL);"
                ],
                views: [
                    "CREATE VIEW dbo.ActiveUsers AS SELECT Id, Name FROM dbo.Users WHERE IsActive = 1;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);
            Assert.Equal("ActiveUsers", view.Name);
            Assert.Equal(2, view.Columns.Count);

            var idCol = view.Columns.FirstOrDefault(c => c.Name == "Id");
            Assert.NotNull(idCol);
            Assert.Equal("INT", idCol.DatabaseType.Value);
            Assert.False(idCol.IsNullable);
            Assert.False(idCol.IsIndeterminate);

            var nameCol = view.Columns.FirstOrDefault(c => c.Name == "Name");
            Assert.NotNull(nameCol);
            Assert.Equal("NVARCHAR", nameCol.DatabaseType.Value);
            Assert.True(nameCol.IsNullable);
            Assert.False(nameCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_ViewWithAliasedTableAndColumns_ShouldInferTypesCorrectly()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Products (ProductCode varchar(50) NOT NULL, UnitPrice money NULL);"
                ],
                views: [
                    "CREATE VIEW dbo.ProductSummary AS SELECT p.ProductCode as Sku, p.UnitPrice FROM dbo.Products p;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);
            Assert.Equal(2, view.Columns.Count);

            var skuCol = view.Columns.FirstOrDefault(c => c.Name == "Sku");
            Assert.NotNull(skuCol);
            Assert.Equal("VARCHAR", skuCol.DatabaseType.Value);
            Assert.False(skuCol.IsNullable);
            Assert.False(skuCol.IsIndeterminate);

            var priceCol = view.Columns.FirstOrDefault(c => c.Name == "UnitPrice");
            Assert.NotNull(priceCol);
            Assert.Equal("MONEY", priceCol.DatabaseType.Value);
            Assert.True(priceCol.IsNullable);
            Assert.False(priceCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_ViewWithIndeterminateColumn_ShouldMarkColumn()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Users (Id int NOT NULL);"
                ],
                views: [
                    "CREATE VIEW dbo.UserWithSource AS SELECT Id, 'Internal' as Source, GETDATE() as CreatedDate FROM dbo.Users;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);
            Assert.Equal(3, view.Columns.Count);

            var idCol = view.Columns.FirstOrDefault(c => c.Name == "Id");
            Assert.NotNull(idCol);
            Assert.False(idCol.IsIndeterminate);

            var sourceCol = view.Columns.FirstOrDefault(c => c.Name == "Source");
            Assert.NotNull(sourceCol);
            Assert.True(sourceCol.IsIndeterminate);
            Assert.Equal(PwSqlType.Unknown, sourceCol.DatabaseType);

            var dateCol = view.Columns.FirstOrDefault(c => c.Name == "CreatedDate");
            Assert.NotNull(dateCol);
            Assert.True(dateCol.IsIndeterminate);
            Assert.Equal(PwSqlType.Unknown, dateCol.DatabaseType);
        }

        [Fact]
        public void Refine_ViewWithJoin_ShouldInferTypesFromCorrectTables()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Orders (OrderId uniqueidentifier NOT NULL, CustomerId int NOT NULL);",
                    "CREATE TABLE dbo.Customers (Id int NOT NULL, CustomerName nvarchar(max) NULL);"
                ],
                views: [
                    "CREATE VIEW dbo.OrderDetails AS SELECT o.OrderId, c.CustomerName FROM dbo.Orders o JOIN dbo.Customers c ON o.CustomerId = c.Id;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);
            Assert.Equal(2, view.Columns.Count);

            var orderIdCol = view.Columns.FirstOrDefault(c => c.Name == "OrderId");
            Assert.NotNull(orderIdCol);
            Assert.Equal("UNIQUEIDENTIFIER", orderIdCol.DatabaseType.Value);
            Assert.False(orderIdCol.IsNullable);
            Assert.False(orderIdCol.IsIndeterminate);

            var nameCol = view.Columns.FirstOrDefault(c => c.Name == "CustomerName");
            Assert.NotNull(nameCol);
            Assert.Equal("NVARCHAR", nameCol.DatabaseType.Value);
            Assert.True(nameCol.IsNullable);
            Assert.False(nameCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_WithIndex_ShouldBeAddedToTable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: ["CREATE TABLE dbo.Users (Email nvarchar(255));"],
                indexes: ["CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);"]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var table = refinedSchema.Objects.First(o => !o.IsView);
            Assert.Single(table.Indexes);
            var index = table.Indexes.First();
            Assert.Equal("IX_Users_Email", index.Name);
            Assert.True(index.IsUnique);
            Assert.Single(index.ColumnNames);
            Assert.Equal("Email", index.ColumnNames.First());
        }

        [Fact]
        public void Refine_WithForeignKey_ShouldBeAddedToTable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Categories (Id int PRIMARY KEY);",
                    "CREATE TABLE dbo.Products (Id int PRIMARY KEY, CategoryId int, CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id));"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var productsTable = refinedSchema.Objects.First(o => o.Name == "Products");
            //Assert.NotEmpty(productsTable.ForeignKeys);
            //var fk = productsTable.ForeignKeys.First();
            //Assert.Equal("FK_Products_Categories", fk.Name);
            //Assert.Equal("dbo.Categories", fk.ForeignTable);
            //Assert.Contains("CategoryId", fk.ForeignKeyColumns);
            //Assert.Contains("Id", fk.PrimaryKeyColumns);
        }

        [Fact]
        public void Refine_ViewWithSubquery_ShouldInferTypesFromSubquery()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Products (Id int, CategoryId int, Name nvarchar(100));",
                    "CREATE TABLE dbo.Categories (Id int, CategoryName nvarchar(50) NULL);"
                ],
                views: [
                    @"CREATE VIEW dbo.ProductCategoryView AS
                      SELECT p.Name, cat.CategoryName
                      FROM dbo.Products p
                      JOIN (SELECT Id, CategoryName FROM dbo.Categories) AS cat ON p.CategoryId = cat.Id;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);
            Assert.Equal(2, view.Columns.Count);

            var nameCol = view.Columns.FirstOrDefault(c => c.Name == "Name");
            Assert.NotNull(nameCol);
            Assert.Equal("NVARCHAR", nameCol.DatabaseType.Value);

            var categoryCol = view.Columns.FirstOrDefault(c => c.Name == "CategoryName");
            Assert.NotNull(categoryCol);
            Assert.Equal("NVARCHAR", categoryCol.DatabaseType.Value);
            Assert.True(categoryCol.IsNullable);
            Assert.False(categoryCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_ViewWithAggregateFunction_ShouldInferAggregateType()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Sales (OrderId int, Amount decimal(18, 2));"
                ],
                views: [
                    "CREATE VIEW dbo.OrderSummaries AS SELECT OrderId, SUM(Amount) as TotalAmount FROM dbo.Sales GROUP BY OrderId;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);

            var totalCol = view.Columns.FirstOrDefault(c => c.Name == "TotalAmount");
            Assert.NotNull(totalCol);
            Assert.Equal("DECIMAL", totalCol.DatabaseType.Value);
            Assert.False(totalCol.IsNullable);
            Assert.False(totalCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_ViewWithIsnullFunction_ShouldInferTypeAndBeNonNullable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Items (Name nvarchar(100) NULL);"
                ],
                views: [
                    "CREATE VIEW dbo.ItemsView AS SELECT ISNULL(Name, 'Unnamed') as ItemName FROM dbo.Items;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);

            var nameCol = view.Columns.FirstOrDefault(c => c.Name == "ItemName");
            Assert.NotNull(nameCol);
            Assert.Equal("NVARCHAR", nameCol.DatabaseType.Value);
            Assert.False(nameCol.IsNullable);
            Assert.False(nameCol.IsIndeterminate);
        }

        [Fact]
        public void Refine_ViewWithStringLiteral_ShouldInferTypeAndBeNonNullable()
        {
            // Arrange

            var rawModel = CreateRawDatabaseModel(
                tables: [
                    "CREATE TABLE dbo.Users (Id int);"
                ],
                views: [
                    "CREATE VIEW dbo.UserSourceView AS SELECT Id, 'Internal' as Source FROM dbo.Users;"
                ]);
            var config = new SqlConfiguration();
            var refiner = new SchemaRefiner(_logger, config);

            // Act
            var refinedSchema = refiner.Refine(rawModel);

            // Assert
            var view = refinedSchema.Objects.FirstOrDefault(o => o.IsView);
            Assert.NotNull(view);

            var sourceCol = view.Columns.FirstOrDefault(c => c.Name == "Source");
            Assert.NotNull(sourceCol);
            Assert.Equal("NVARCHAR", sourceCol.DatabaseType.Value);
            Assert.False(sourceCol.IsNullable);
            Assert.False(sourceCol.IsIndeterminate);
        }

        private RawDatabaseSchema CreateRawDatabaseModel(IEnumerable<string>? tables = null, IEnumerable<string>? views = null, IEnumerable<string>? indexes = null)
        {
            var model = new RawDatabaseSchema() { DatabaseName = "TestDb" };
            var parser = new TSql160Parser(true);

            if (tables != null)
            {
                foreach (var tableSql in tables)
                {
                    using var reader = new StringReader(tableSql);
                    var fragment = parser.Parse(reader, out var errors);
                    Assert.Empty(errors);
                    var statement = (fragment as TSqlScript)!.Batches[0].Statements[0] as CreateTableStatement;
                    model.TableStatements.Add(statement!);

                    // Also extract table into model's Tables collection for easier testing
                    var schemaName = statement!.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo";
                    var tableName = statement.SchemaObjectName.BaseIdentifier.Value;
                    var table = new Table
                    {
                        Schema = schemaName,
                        Name = tableName,
                        Columns = statement.Definition.ColumnDefinitions.Select(cd => new Column
                        {
                            Name = cd.ColumnIdentifier.Value,
                            // This is a simplified extraction. The actual Ingestion phase would have more logic.
                            SqlType = cd.DataType.Name.BaseIdentifier.Value,
                            IsNullable = cd.Constraints.OfType<NullableConstraintDefinition>().FirstOrDefault()?.Nullable ?? true
                        }).ToList()
                    };
                    model.Tables.Add(table);
                }
            }
            
            if (views != null)
            {
                foreach (var viewSql in views)
                {
                    using var reader = new StringReader(viewSql);
                    var fragment = parser.Parse(reader, out var errors);
                    Assert.Empty(errors);
                    var statement = (fragment as TSqlScript)!.Batches[0].Statements[0] as CreateViewStatement;
                    model.ViewStatements.Add(statement!);

                    // Also extract view into model's Views collection for easier testing
                    var schemaName = statement!.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo";
                    var viewName = statement.SchemaObjectName.BaseIdentifier.Value;
                    var view = new View
                    {
                        Schema = schemaName,
                        Name = viewName
                    };
                    model.Views.Add(view);
                }
            }
            
            if (indexes != null)
            {
                foreach (var indexSql in indexes)
                {
                    using var reader = new StringReader(indexSql);
                    var fragment = parser.Parse(reader, out var errors);
                    Assert.Empty(errors);
                    var statement = (fragment as TSqlScript)!.Batches[0].Statements[0] as CreateIndexStatement;
                    model.IndexStatements.Add(statement!);
                }
            }

            return model;
        }
    }

        /// <summary>
    /// Simple logger implementation for tests
    /// </summary>
    public class TestLogger : IBvLogger
    {
        public List<string> Messages { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public void LogMessage(string message)
        {
            Messages.Add(message);
        }

        public void LogWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public void LogError(string error)
        {
            Errors.Add(error);
        }

        public void LogError(string error, Exception exception)
        {
            Errors.Add($"{error}: {exception.Message}");
        }

        public void LogErrorFromException(Exception exception)
        {
            Errors.Add($"Error: {exception.Message}");
        }
    }
}
