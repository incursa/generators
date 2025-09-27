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

using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator._1_Ingestion;

public class SqlSchemaIngestorTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void IngestSchema_WithCreateTable_ShouldParseCorrectly()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE MyTable (
                Id INT PRIMARY KEY,
                Name NVARCHAR(100)
            );
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.TableStatements);
        Assert.Empty(schema.ViewStatements);
        
        // Detailed assertions on the parsed table statement
        var tableStatement = schema.TableStatements[0];
        Assert.Equal("MyTable", tableStatement.SchemaObjectName.BaseIdentifier.Value);
        Assert.Null(tableStatement.SchemaObjectName.SchemaIdentifier); // No schema specified = null
        Assert.Equal(2, tableStatement.Definition.ColumnDefinitions.Count);
        
        // Check first column (Id)
        var idColumn = tableStatement.Definition.ColumnDefinitions[0];
        Assert.Equal("Id", idColumn.ColumnIdentifier.Value);
        Assert.Equal("int", idColumn.DataType.Name.BaseIdentifier.Value.ToLower());
        
        // Check second column (Name)
        var nameColumn = tableStatement.Definition.ColumnDefinitions[1];
        Assert.Equal("Name", nameColumn.ColumnIdentifier.Value);
        Assert.Equal("nvarchar", nameColumn.DataType.Name.BaseIdentifier.Value.ToLower());
    }

    [Fact]
    public void IngestSchema_WithCreateView_ShouldParseCorrectly()
    {
        // Arrange
        var sqlText = """
            CREATE VIEW MyView AS
            SELECT Id, Name FROM MyTable;
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Empty(schema.TableStatements);
        Assert.Single(schema.ViewStatements);
        
        // Detailed assertions on the parsed view statement
        var viewStatement = schema.ViewStatements[0];
        Assert.Equal("MyView", viewStatement.SchemaObjectName.BaseIdentifier.Value);
        Assert.Null(viewStatement.SchemaObjectName.SchemaIdentifier); // No schema specified = null
        Assert.NotNull(viewStatement.SelectStatement);
    }

    [Fact]
    public void IngestSchema_WithCreateIndex_ShouldParseCorrectly()
    {
        // Arrange
        var sqlText = """
            CREATE INDEX IX_MyTable_Name ON MyTable (Name);
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.IndexStatements);
        
        // Detailed assertions on the parsed index statement
        var indexStatement = schema.IndexStatements[0];
        Assert.Equal("IX_MyTable_Name", indexStatement.Name.Value);
        Assert.Equal("MyTable", indexStatement.OnName.BaseIdentifier.Value);
        Assert.Single(indexStatement.Columns); // One column in the index
        Assert.Equal("Name", indexStatement.Columns[0].Column.MultiPartIdentifier.Identifiers[0].Value);
    }

    [Fact]
    public void IngestSchema_WithMultipleObjects_ShouldParseAllObjects()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE MyTable (Id INT);
            GO
            CREATE VIEW MyView AS SELECT Id FROM MyTable;
            GO
            CREATE INDEX IX_MyTable_Id ON MyTable (Id);
            GO
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.TableStatements);
        Assert.Single(schema.ViewStatements);
        Assert.Single(schema.IndexStatements);
    }

    [Fact]
    public void IngestSchema_WithInvalidSql_ShouldLogErrors()
    {
        // Arrange
        var sqlText = "CREATE TABLE;";
        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.True(_logger.ErrorMessages.Count > 0);
    }

    [Fact]
    public void IngestSchema_WithMultipleScripts_ShouldParseAllObjects()
    {
        // Arrange
        var script1 = """
            CREATE TABLE Table1 (
                Id INT PRIMARY KEY,
                Name NVARCHAR(50)
            );
            GO
            """;
        
        var script2 = """
            CREATE TABLE Table2 (
                Id INT PRIMARY KEY,
                Description NVARCHAR(100)
            );
            GO
            CREATE VIEW View1 AS SELECT * FROM Table1;
            GO
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { script1, script2 });

        // Assert
        Assert.NotNull(schema);
        Assert.Equal(2, schema.TableStatements.Count); // Two tables from both scripts
        Assert.Single(schema.ViewStatements); // One view from script2
        
        // Verify objects from different scripts are all present
        var tableNames = schema.TableStatements.Select(t => t.SchemaObjectName.BaseIdentifier.Value).ToArray();
        Assert.Contains("Table1", tableNames);
        Assert.Contains("Table2", tableNames);
        
        var viewNames = schema.ViewStatements.Select(v => v.SchemaObjectName.BaseIdentifier.Value).ToArray();
        Assert.Contains("View1", viewNames);
    }

    [Fact]
    public void IngestSchema_WithEmptyScripts_ShouldReturnEmptySchema()
    {
        // Arrange
        var emptyScripts = new[] { "", "   ", "\n\t" };
        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(emptyScripts);

        // Assert
        Assert.NotNull(schema);
        Assert.Empty(schema.TableStatements);
        Assert.Empty(schema.ViewStatements);
        Assert.Empty(schema.IndexStatements);
        Assert.Empty(_logger.ErrorMessages); // Should not generate errors
    }

    [Fact]
    public void IngestSchema_WithNoScripts_ShouldReturnEmptySchema()
    {
        // Arrange
        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new string[0]);

        // Assert
        Assert.NotNull(schema);
        Assert.Empty(schema.TableStatements);
        Assert.Empty(schema.ViewStatements);
        Assert.Empty(schema.IndexStatements);
        Assert.Empty(_logger.ErrorMessages); // Should not generate errors
    }

    [Fact]
    public void IngestSchema_WithGoBatchSeparators_ShouldParseAllBatches()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE Table1 (Id INT);
            GO
            CREATE TABLE Table2 (Id INT);
            GO
            CREATE VIEW View1 AS SELECT * FROM Table1;
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Equal(2, schema.TableStatements.Count); // Both tables should be parsed
        Assert.Single(schema.ViewStatements); // View should be parsed
        
        var tableNames = schema.TableStatements.Select(t => t.SchemaObjectName.BaseIdentifier.Value).ToArray();
        Assert.Contains("Table1", tableNames);
        Assert.Contains("Table2", tableNames);
    }

    [Fact]
    public void IngestSchema_WithUnsupportedStatements_ShouldIgnoreThemSafely()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE MyTable (Id INT);
            GO
            ALTER TABLE MyTable ADD [Name] NVARCHAR(50);
            GO
            CREATE PROCEDURE MyProc AS SELECT * FROM MyTable;
            GO
            GRANT SELECT ON MyTable TO SomeRole;
            GO
            CREATE VIEW MyView AS SELECT * FROM MyTable;
            GO
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.TableStatements); // Only the CREATE TABLE should be captured
        Assert.Single(schema.ViewStatements);  // Only the CREATE VIEW should be captured
        Assert.Empty(schema.IndexStatements);
        
        // Should not have crashed or generated errors for unsupported statements
        Assert.Empty(_logger.ErrorMessages);
        
        // Verify the captured objects are correct
        Assert.Equal("MyTable", schema.TableStatements[0].SchemaObjectName.BaseIdentifier.Value);
        Assert.Equal("MyView", schema.ViewStatements[0].SchemaObjectName.BaseIdentifier.Value);
    }

    [Fact]
    public void IngestSchema_WithSchemaQualifiedNames_ShouldParseSchemaCorrectly()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE dbo.MyTable (
                Id INT PRIMARY KEY,
                Name NVARCHAR(100)
            );
            GO
            CREATE VIEW sales.MyView AS SELECT * FROM dbo.MyTable;
            GO
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.TableStatements);
        Assert.Single(schema.ViewStatements);
        
        // Check table schema qualification
        var tableStatement = schema.TableStatements[0];
        Assert.Equal("MyTable", tableStatement.SchemaObjectName.BaseIdentifier.Value);
        Assert.Equal("dbo", tableStatement.SchemaObjectName.SchemaIdentifier.Value);
        
        // Check view schema qualification
        var viewStatement = schema.ViewStatements[0];
        Assert.Equal("MyView", viewStatement.SchemaObjectName.BaseIdentifier.Value);
        Assert.Equal("sales", viewStatement.SchemaObjectName.SchemaIdentifier.Value);
    }

    [Fact]
    public void IngestSchema_WithComplexTableDefinition_ShouldParseAllElements()
    {
        // Arrange
        var sqlText = """
            CREATE TABLE dbo.Orders (
                OrderId INT IDENTITY(1,1) PRIMARY KEY,
                CustomerId INT NOT NULL,
                OrderDate DATETIME2 NULL,
                TotalAmount DECIMAL(10,2) NOT NULL DEFAULT 0.00,
                Notes NVARCHAR(MAX)
            );
            """;

        var ingestor = new SqlSchemaIngestor(_logger);

        // Act
        var schema = ingestor.Ingest(new[] { sqlText });

        // Assert
        Assert.NotNull(schema);
        Assert.Single(schema.TableStatements);
        
        var tableStatement = schema.TableStatements[0];
        Assert.Equal("Orders", tableStatement.SchemaObjectName.BaseIdentifier.Value);
        Assert.Equal("dbo", tableStatement.SchemaObjectName.SchemaIdentifier.Value);
        Assert.Equal(5, tableStatement.Definition.ColumnDefinitions.Count);
        
        // Verify specific column details
        var orderIdColumn = tableStatement.Definition.ColumnDefinitions[0];
        Assert.Equal("OrderId", orderIdColumn.ColumnIdentifier.Value);
        Assert.Equal("int", orderIdColumn.DataType.Name.BaseIdentifier.Value.ToLower());
        
        var totalAmountColumn = tableStatement.Definition.ColumnDefinitions[3];
        Assert.Equal("TotalAmount", totalAmountColumn.ColumnIdentifier.Value);
        Assert.Equal("decimal", totalAmountColumn.DataType.Name.BaseIdentifier.Value.ToLower());
    }
}
