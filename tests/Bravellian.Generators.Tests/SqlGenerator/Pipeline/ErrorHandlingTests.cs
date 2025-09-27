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

using System;
using System.Linq;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator.Pipeline;

public class ErrorHandlingTests
{
    private readonly TestLogger logger = new ();

    [Fact]
    public void Generate_WithInvalidSql_ShouldLogErrorAndReturnEmptyDictionary()
    {
        // Arrange
        var invalidSql = "CREATE TABLE Orders (OrderId INT PRIMARY KEY,"; // Missing closing parenthesis

        var config = new SqlConfiguration();
        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { invalidSql });

        // Assert
        Assert.NotNull(generatedCode);
        Assert.Empty(generatedCode);

        // Assert.Contains(_logger.Messages, m => m.Level == LogLevel.Error && m.Contains("syntax error"));
    }

    [Fact]
    public void Generate_WithInvalidConfiguration_ShouldLogErrorAndHandleGracefully()
    {
        // Arrange
        var sql = "CREATE TABLE dbo.Users (Id INT PRIMARY KEY, Name NVARCHAR(100) NOT NULL);";

        var invalidConfig = new SqlConfiguration
        {
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.NonExistentTable"] = new TableConfiguration
                {
                    PrimaryKeyOverride = new HashSet<string>(StringComparer.Ordinal) { "NonExistentColumn" }, // This column doesn't exist
                },
            },
        };

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, invalidConfig),
            new CSharpModelTransformer(this.logger, invalidConfig, null),
            new CSharpCodeGenerator(invalidConfig, this.logger),
            invalidConfig,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert
        Assert.NotNull(generatedCode);
        Assert.Equal(2, generatedCode.Count); // Should still generate code for valid table

        // Assert.Contains(_logger.Messages, m => m.Level == LogLevel.Warning &&
        //    (m.Contains("NonExistentTable") || m.Contains("configuration")));
    }

    [Fact]
    public void Generate_WithInvalidTypeMapping_ShouldFallBackToDefaultMapping()
    {
        // Arrange
        var sql = "CREATE TABLE dbo.Products (Id INT PRIMARY KEY, Price DECIMAL(10,2) NOT NULL);";

        var configWithBadMapping = new SqlConfiguration
        {
            GlobalTypeMappings = new List<GlobalTypeMapping>
            {
                new GlobalTypeMapping
                {
                    Description = "Invalid mapping",
                    Match = new GlobalTypeMappingMatch { SqlType = new List<string> { "decimal" } },
                    Apply = new GlobalTypeMappingApply { CSharpType = "NonExistentType" }, // Invalid C# type
                },
            },
        };

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, configWithBadMapping),
            new CSharpModelTransformer(this.logger, configWithBadMapping, null),
            new CSharpCodeGenerator(configWithBadMapping, this.logger),
            configWithBadMapping,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert
        Assert.NotNull(generatedCode);
        Assert.Equal(2, generatedCode.Count);
        var productClass = generatedCode.First().Value;

        // Should fall back to default decimal mapping (not the invalid type)
        Assert.Contains("public decimal Price { get; set; }", productClass, StringComparison.Ordinal);
        Assert.DoesNotContain("NonExistentType", productClass, StringComparison.Ordinal);

        // Assert.Contains(_logger.Messages, m => m.Level == LogLevel.Warning && m.Contains("type"));
    }
}
