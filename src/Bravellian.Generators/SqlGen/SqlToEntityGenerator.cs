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

#if false

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
// using Bravellian.Generators.SqlGen.Pipeline.CodeGeneration;

/// <summary>
/// Main orchestrator for SQL to entity generation process.
/// This class ties together the different phases of the generation process.
/// NOTE: This is a legacy class. Use SqlGenOrchestrator for new implementations.
/// </summary>
[Obsolete("Use SqlGenOrchestrator instead")]
public class SqlToEntityGenerator
{
    private readonly IPwLogger logger;
    private readonly ISchemaIngestor schemaIngestor;
    private readonly ITypeResolver typeResolver;
    // private readonly ICodeModelMapper codeGenerator;
    // private readonly ICodeRenderer codeRenderer;

    /// <summary>
    /// Creates a new SQL to entity generator.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="typeMappingConfigPath">The path to the type mapping configuration file.</param>
    /// <param name="debugTargets">Optional comma-separated list of debug targets in the format schema.table.column.</param>
    /// <param name="generateNavigationProperties">Whether to generate navigation properties.</param>
    public SqlToEntityGenerator(
        IPwLogger logger,
        string typeMappingConfigPath,
        string? debugTargets = null,
        bool generateNavigationProperties = true)
    {
        this.logger = logger;

        // Create the individual components
        this.schemaIngestor = new SqlSchemaIngestor(logger);
        this.typeResolver = new SqlTypeResolver(logger);

        // Create C# specific components
        var typeMapper = new CSharpTypeMapper();
        this.codeGenerator = new CSharpModelMapper(typeMapper, logger, new(), generateNavigationProperties);
        this.codeRenderer = new CSharpCodeRenderer();
    }


    public SqlToEntityGenerator(IPwLogger logger)
    {
        this.logger = logger;

        var typeMapConfig = new CodeGeneration.TypeMappingConfiguration();

        // Create the individual components
        this.schemaIngestor = new SqlSchemaIngestor(logger);
        this.typeResolver = new SqlTypeResolver(logger);

        // Create C# specific components
        var typeMapper = new CSharpTypeMapper();
        this.codeGenerator = new CSharpModelMapper(typeMapper, logger, typeMapConfig, true);
        this.codeRenderer = new CSharpCodeRenderer();
    }

    public SqlToEntityGenerator(IPwLogger logger, CodeGeneration.TypeMappingConfiguration typeMappingConfiguration)
    {
        this.logger = logger;

        // Create the individual components
        this.schemaIngestor = new SqlSchemaIngestor(logger);
        this.typeResolver = new SqlTypeResolver(logger);

        // Create C# specific components
        var typeMapper = new CSharpTypeMapper();
        this.codeGenerator = new CSharpModelMapper(typeMapper, logger, typeMappingConfiguration, true);
        this.codeRenderer = new CSharpCodeRenderer();
    }

    /// <summary>
    /// Generates entities from a collection of SQL files.
    /// </summary>
    /// <param name="sqlFilePaths">The paths to the SQL files to process.</param>
    /// <param name="outputDirectory">The directory to output the generated files to.</param>
    /// <param name="baseNamespace">The base namespace for the generated entities.</param>
    /// <param name="databaseName">Optional database name.</param>
    /// <param name="dbTypeMappingFilePath">Optional path to a database type mapping file.</param>
    /// <returns>A list of generated file paths.</returns>
    public List<string> GenerateEntitiesFromSqlFiles(
        IEnumerable<string> sqlFilePaths,
        string outputDirectory,
        string baseNamespace,
        string? databaseName = null,
        string? dbTypeMappingFilePath = null)
    {
        // Phase 1: Ingest schema from SQL files
        this.logger.LogMessage("Phase 1: Ingesting schema from SQL files...");
        var rawSchema = this.schemaIngestor.IngestSchemaFromFiles(sqlFilePaths, databaseName);

        this.logger.LogMessage($"Found {rawSchema.TableStatements.Count} tables and {rawSchema.ViewStatements.Count} views.");

        // Phase 2: Resolve types
        this.logger.LogMessage("Phase 2: Resolving database types...");
        var databaseSchema = this.typeResolver.Resolve(rawSchema);

        this.logger.LogMessage($"Created database schema with {databaseSchema.Objects.Count} objects.");

        // Phase 3: Generate code
        this.logger.LogMessage("Phase 3: Generating entity classes...");
        var models = this.codeGenerator.MapToCodeModel(databaseSchema, baseNamespace);

        // Phase 4: Render to file
        var generatedFiles = new List<string>();
        foreach (var model in models)
        {
            var file = this.codeRenderer.RenderCode(model);
            generatedFiles.Add(file);
        }

        this.logger.LogMessage($"Generated {generatedFiles.Count} entity classes.");
        return generatedFiles;
    }
}

#endif

