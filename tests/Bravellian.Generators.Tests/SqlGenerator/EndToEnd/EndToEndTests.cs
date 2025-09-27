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

using System.Text.Json;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator.EndToEnd;

public class EndToEndTests
{
    private readonly TestLogger logger = new ();

    [Fact]
    public void Generate_FromSql_ShouldProduceCorrectCSharp()
    {
        // Arrange
        var sql = """
            CREATE TABLE Users (
                Id INT PRIMARY KEY,
                Username NVARCHAR(50) NOT NULL
            );
            """;
        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, null),
            new CSharpModelTransformer(this.logger, null, null),
            new CSharpCodeGenerator(null, this.logger),
            null,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());
        var userClass = generatedCode.First(c => c.Key.EndsWith("Users.cs", StringComparison.Ordinal));
        Assert.Contains("public class Users", userClass.Value, StringComparison.Ordinal);
        Assert.Contains("public int Id { get; set; }", userClass.Value, StringComparison.Ordinal);
        Assert.Contains("public string Username { get; set; }", userClass.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateComplex_FromSql_ShouldProduceCorrectCSharp()
    {
        // Arrange
        var sql = """
            CREATE TABLE [erp].[TaxCode] (
                [BravellianTenantId] BIGINT NOT NULL,
                [TaxCodeId] BIGINT NOT NULL,
                [ErpId] VARCHAR(200) NOT NULL,
                [Description] VARCHAR(200) NULL,
                [Code] VARCHAR(100) NULL,
                [Rate] DECIMAL(18,2) NULL,
                [ModifiedOn] DATETIMEOFFSET(7) NOT NULL,
                [SourceCreateOn] DATETIMEOFFSET(7) NULL,
                [SourceUpdatedOn] DATETIMEOFFSET(7) NULL,
                [RecordSource] VARCHAR(50) NOT NULL CONSTRAINT [DF_erp_TaxCode_RecordSource] DEFAULT ('erp'),
                [RowFingerprint] NVARCHAR(128) NULL,
                CONSTRAINT [PK_erp_TaxCode] PRIMARY KEY ([BravellianTenantId] ASC, [TaxCodeId] ASC),
                CONSTRAINT [FK_erp_TaxCode_BravellianTenantId] FOREIGN KEY ([BravellianTenantId]) REFERENCES [pw].[BravellianTenant] (
                    [BravellianTenantId]
                )
            )
            GO

            CREATE UNIQUE INDEX [UQ_erp_TaxCode_BravellianTenantId_ErpId]
                ON [erp].[TaxCode] ([BravellianTenantId], [ErpId]);
            GO

            -- ================================================================
            -- FOREIGN KEY SUPPORT INDEXES
            -- ================================================================

            /*
               Rationale: No additional foreign key indexes needed.
               TaxCode table only has FK to BravellianTenant which is covered by the PK.
            */

            -- ================================================================
            -- PERFORMANCE OPTIMIZATION INDEXES
            -- ================================================================

            /*
               Rationale: Supports tax code lookups in line item detail views that reference
               tax codes. Provides covering for tax calculations.
            */
            CREATE NONCLUSTERED INDEX [IX_TaxCode_BravellianTenantId_TaxCodeId_Covering]
                ON [erp].[TaxCode] ([BravellianTenantId], [TaxCodeId])
                INCLUDE ([Code], [Description], [Rate]);
            GO

            """;

        var config = new SqlConfiguration
        {
            DbContextBaseClass = "PwDbContextBase",
            GenerateDbContext = true,
            Namespace = "Bravellian.Database.PwSql",
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["erp.TaxCode"] = new ()
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>(
StringComparer.Ordinal)
                    {
                        ["ErpId"] = new ()
                        {
                            CSharpType = "TaxCodeErpIdentifier",
                        },
                    },
                },
            },
            GlobalTypeMappings = [
                new ()
                {
                    Match = new ()
                    {
                        ColumnNameRegex = "BravellianTenantId",
                    },
                    Apply = new ()
                    {
                        CSharpType = "BravellianTenantIdentifier",
                    },
                },
                new ()
                {
                    Match = new ()
                    {
                        ColumnNameRegex = "TaxCodeId",
                        SqlType = ["bigint"],
                    },
                    Apply = new ()
                    {
                        CSharpType = "TaxCodeIdentifier",
                    },
                }

            ],
        };

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        // var json = JsonSerializer.Serialize(config, options);
        // config = JsonSerializer.Deserialize<SqlConfiguration>(json, options);
        var json = $$"""
            {
                "namespace": "Bravellian.Database.PwSql",
                "generateNavigationProperties": true,
                "generateDbContext": true,
                "dbContextBaseClass": "PwDbContextBase",
                "globalTypeMappings": [
                    {
                        "match": {
                            "columnNameRegex": "BravellianTenantId"
                        },
                        "apply": {
                            "csharpType": "BravellianTenantIdentifier"
                        }
                    },
                    {
                        "description": null,
                        "priority": null,
                        "match": {
                            "columnNameRegex": "TaxCodeId",
                            "tableNameRegex": null,
                            "schemaNameRegex": null,
                            "sqlType": "bigint"
                        },
                        "apply": {
                            "csharpType": "TaxCodeIdentifier"
                        }
                    }
                ],
                "tables": {
                    "erp.TaxCode": {
                        "columnOverrides": {
                            "ErpId": {
                                "csharpType": "TaxCodeErpIdentifier"
                            }
                        }
                    }
                }
            }
            """;

        config = JsonSerializer.Deserialize<SqlConfiguration>(json, options);

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config!, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(new[] { sql });

        // Assert
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());

        // var userClass = generatedCode.First(c => c.Key.EndsWith("Users.cs"));
        // Assert.Contains("public class Users", userClass.Value);
        // Assert.Contains("public int Id { get; set; }", userClass.Value);
        // Assert.Contains("public string Username { get; set; }", userClass.Value);
    }

    [Fact]
    public void GenerateFull_FromSql_ShouldProduceCorrectCSharp()
    {
        var configJson = File.ReadAllText(@"C:\src\internal\src\modules\Database\Bravellian.Database.PwSql.Model\sql.generator.config.json");
        var sqlFiles = Directory.GetFiles(@"C:\src\internal\src\modules\Database\Bravellian.Database.PwSql\", "*.sql", SearchOption.AllDirectories)
            .Select(f => File.ReadAllText(f)).ToArray();

        var config = SqlConfiguration.FromJson(configJson);

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(sqlFiles);

        // Assert
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());

        // var userClass = generatedCode.First(c => c.Key.EndsWith("Users.cs"));
        // Assert.Contains("public class Users", userClass.Value);
        // Assert.Contains("public int Id { get; set; }", userClass.Value);
        // Assert.Contains("public string Username { get; set; }", userClass.Value);
    }

    [Fact]
    public void GenerateFull_FromSingleSql_ShouldProduceCorrectCSharp()
    {
        var configJson = File.ReadAllText(@"C:\src\internal\src\modules\Database\Bravellian.Database.PwSql.Model\sql.generator.config.json");
        string[] sqlFiles = [
            File.ReadAllText(@"C:\src\internal\src\modules\Database\Bravellian.Database.Audit\events\Tables\AuditEntry.sql"),

            // File.ReadAllText(@"C:\src\internal\src\modules\Database\Bravellian.Database.PwSql\erp\Tables\ApInvoice.sql"),
            // File.ReadAllText(@"C:\src\internal\src\modules\Database\Bravellian.Database.PwSql\erp\Views\ApInvoiceDetail.sql"),
        ];

        var config = SqlConfiguration.FromJson(configJson);

        var orchestrator = new SqlGenOrchestrator(
            new SqlSchemaIngestor(this.logger),
            new SchemaRefiner(this.logger, config),
            new CSharpModelTransformer(this.logger, config, null),
            new CSharpCodeGenerator(config, this.logger),
            config,
            this.logger);

        // Act
        var generatedCode = orchestrator.Generate(sqlFiles);

        // Assert
        Assert.NotNull(generatedCode);
        Assert.True(generatedCode.Any());

        // var userClass = generatedCode.First(c => c.Key.EndsWith("Users.cs"));
        // Assert.Contains("public class Users", userClass.Value);
        // Assert.Contains("public int Id { get; set; }", userClass.Value);
        // Assert.Contains("public string Username { get; set; }", userClass.Value);
    }
}
