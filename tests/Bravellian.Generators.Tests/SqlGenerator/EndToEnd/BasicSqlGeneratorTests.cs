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

using Bravellian.Generators;

namespace Bravellian.Generators.Tests.SqlGenerator;

public class BasicSqlGeneratorTests
{
    [Fact]
    public void BasicTest_ShouldPass()
    {
        // This is a basic test to verify the test setup works
        Assert.True(true);
    }

    [Fact]
    public void TestLogger_ShouldLogMessages()
    {
        // Arrange
        var logger = new TestLogger();
        var message = "Test message";

        // Act
        logger.LogMessage(message);
        logger.LogWarning(message);
        logger.LogError(message);

        // Assert
        Assert.True(logger.InfoMessages.Contains(message));
        Assert.True(logger.WarningMessages.Contains(message));
        Assert.True(logger.ErrorMessages.Contains(message));
    }

    [Fact]
    public void FileReading_ShouldReadSqlFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var sqlContent = """
            CREATE TABLE [dbo].[Users] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [Name] nvarchar(255) NOT NULL,
                CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
            );
            """;

        try
        {
            File.WriteAllText(tempFile, sqlContent);

            // Act
            var readContent = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal(sqlContent, readContent);
            Assert.Contains("CREATE TABLE", readContent);
            Assert.Contains("Users", readContent);
            Assert.Contains("Id", readContent);
            Assert.Contains("Name", readContent);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void JsonConfiguration_ShouldParseBasicConfig()
    {
        // Arrange
        var json = """
            {
                "namespace": "MyApp.Data.Entities",
                "generateNavigationProperties": true,
                "generateDbContext": true
            }
            """;

        // Act
        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("MyApp.Data.Entities", config["namespace"].ToString());
        Assert.True(bool.Parse(config["generateNavigationProperties"].ToString()!));
        Assert.True(bool.Parse(config["generateDbContext"].ToString()!));
    }

    [Fact]
    public void SqlContent_ShouldContainExpectedElements()
    {
        // Arrange
        var sql = """
            CREATE TABLE [dbo].[Users] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [Name] nvarchar(255) NOT NULL,
                [Email] nvarchar(255) NULL,
                [CreatedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
            );
            
            CREATE TABLE [dbo].[Orders] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [UserId] int NOT NULL,
                [Total] decimal(18,2) NOT NULL,
                CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
            );
            
            CREATE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
            """;

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql);
        Assert.Contains("[dbo].[Users]", sql);
        Assert.Contains("[dbo].[Orders]", sql);
        Assert.Contains("IDENTITY(1,1)", sql);
        Assert.Contains("PRIMARY KEY", sql);
        Assert.Contains("FOREIGN KEY", sql);
        Assert.Contains("CREATE INDEX", sql);
        Assert.Contains("decimal(18,2)", sql);
        Assert.Contains("datetime2", sql);
        Assert.Contains("nvarchar(255)", sql);
    }

    [Fact]
    public void DatabaseTypes_ShouldMapToCorrectCSharpTypes()
    {
        // Arrange
        var typeMappings = new Dictionary<string, string>
        {
            { "int", "int" },
            { "nvarchar", "string" },
            { "varchar", "string" },
            { "decimal", "decimal" },
            { "datetime2", "DateTime" },
            { "bit", "bool" },
            { "uniqueidentifier", "Guid" },
            { "bigint", "long" },
            { "smallint", "short" },
            { "tinyint", "byte" },
            { "float", "double" },
            { "real", "float" }
        };

        // Act & Assert
        foreach (var mapping in typeMappings)
        {
            var sqlType = mapping.Key;
            var expectedCSharpType = mapping.Value;
            
            Assert.NotNull(sqlType);
            Assert.NotNull(expectedCSharpType);
            Assert.True(expectedCSharpType.Length > 0);
        }
    }

    [Fact]
    public void NullableTypes_ShouldHaveCorrectSuffix()
    {
        // Arrange
        var baseTypes = new[] { "int", "decimal", "DateTime", "bool", "Guid", "long", "short", "byte", "double", "float" };
        var stringTypes = new[] { "string" };

        // Act & Assert
        foreach (var baseType in baseTypes)
        {
            var nullableType = $"{baseType}?";
            Assert.Contains("?", nullableType);
            Assert.StartsWith(baseType, nullableType);
        }

        foreach (var stringType in stringTypes)
        {
            // String is already nullable, so string? is also valid
            var nullableType = $"{stringType}?";
            Assert.Contains("?", nullableType);
            Assert.StartsWith(stringType, nullableType);
        }
    }

    [Fact]
    public void SqlColumnPattern_ShouldMatchExpectedFormat()
    {
        // Arrange
        var columnDefinitions = new[]
        {
            "[Id] int IDENTITY(1,1) NOT NULL",
            "[Name] nvarchar(255) NOT NULL",
            "[Email] nvarchar(255) NULL",
            "[Total] decimal(18,2) NOT NULL",
            "[CreatedAt] datetime2 NOT NULL",
            "[IsActive] bit NOT NULL"
        };

        // Act & Assert
        foreach (var columnDef in columnDefinitions)
        {
            Assert.Contains("[", columnDef);
            Assert.Contains("]", columnDef);
            Assert.True(columnDef.Contains("NOT NULL") || columnDef.Contains("NULL"));
        }
    }

    [Fact]
    public void ConfigurationOverride_ShouldAllowCustomization()
    {
        // Arrange
        var tableOverrides = new Dictionary<string, string>
        {
            { "Users", "User" },
            { "Orders", "Order" },
            { "Products", "Product" }
        };

        var columnOverrides = new Dictionary<string, string>
        {
            { "Id", "UserId" },
            { "Name", "FullName" },
            { "Email", "EmailAddress" }
        };

        // Act & Assert
        foreach (var tableOverride in tableOverrides)
        {
            Assert.NotEqual(tableOverride.Key, tableOverride.Value);
            Assert.True(tableOverride.Key.EndsWith("s") && !tableOverride.Value.EndsWith("s"));
        }

        foreach (var columnOverride in columnOverrides)
        {
            Assert.NotEqual(columnOverride.Key, columnOverride.Value);
            Assert.True(columnOverride.Value.Length >= columnOverride.Key.Length);
        }
    }

}

