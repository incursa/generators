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

namespace Bravellian.Generators.SqlGen.Pipeline.1_Ingestion
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

/// <summary>
/// Implements the schema ingestor interface for SQL Server.
/// </summary>
    public class SqlSchemaIngestor : ISchemaIngestor
{
    private readonly IBvLogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSchemaIngestor"/> class.
    /// Creates a new SQL schema ingestor.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SqlSchemaIngestor(IBvLogger logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public RawDatabaseSchema Ingest(IEnumerable<string> sqlScriptText)
    {
        var databaseModel = new RawDatabaseSchema();

        foreach (var sqlText in sqlScriptText)
        {
            try
            {
                this.logger.LogMessage($"Processing SQL text: {sqlText.Substring(0, Math.Min(100, sqlText.Length))}...");
                var parser = new TSql170Parser(true, SqlEngineType.All);

                using var reader = new StringReader(sqlText);
                var parseResult = parser.Parse(reader, out var errors);

                if (errors != null && errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        this.logger.LogError($"SQL parse error: {error.Message}");
                    }
                }

                if (parseResult is TSqlScript script)
                {
                    this.ProcessSqlScript(script, databaseModel);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Error processing SQL text: {ex.Message}");
            }
        }

        return databaseModel;
    }

    private void ProcessSqlScript(TSqlScript script, RawDatabaseSchema databaseModel)
    {
        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                this.ProcessStatement(statement, databaseModel);
            }
        }
    }

    private void ProcessStatement(TSqlStatement statement, RawDatabaseSchema databaseModel)
    {
        switch (statement)
        {
            case CreateTableStatement createTableStatement:
                var tableName = createTableStatement.SchemaObjectName.BaseIdentifier.Value;
                if (tableName.StartsWith("#", StringComparison.Ordinal) || tableName.StartsWith("@", StringComparison.Ordinal))
                {
                    this.logger.LogMessage($"Ignoring temporary table: {tableName}");
                    break;
                }

                databaseModel.TableStatements.Add(createTableStatement);
                break;
            case CreateViewStatement createViewStatement:
                var viewName = createViewStatement.SchemaObjectName.BaseIdentifier.Value;
                if (viewName.StartsWith("#", StringComparison.Ordinal) || viewName.StartsWith("@", StringComparison.Ordinal))
                {
                    this.logger.LogMessage($"Ignoring temporary view: {viewName}");
                    break;
                }

                databaseModel.ViewStatements.Add(createViewStatement);
                break;
            case CreateIndexStatement createIndexStatement:
                databaseModel.IndexStatements.Add(createIndexStatement);
                break;

            // Add more statement types as needed
            default:
                // Ignore unsupported statement types
                break;
        }
    }
}
}
