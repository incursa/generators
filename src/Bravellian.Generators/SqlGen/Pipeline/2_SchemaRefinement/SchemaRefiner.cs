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

namespace Bravellian.Generators.SqlGen.Pipeline.2_SchemaRefinement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bravellian.Generators.SqlGen.Common;
    using Bravellian.Generators.SqlGen.Common.Configuration;
    using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
    using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
    using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

/// <summary>
/// Implements Phase 2 of the pipeline. This phase is responsible for translating raw SQL statements
/// into a structured database schema and "patching" it with information from the configuration file.
/// Its primary purpose is to fix missing type or nullability information for columns,
/// especially in SQL views, where this cannot be inferred from the DDL.
/// </summary>
    public class SchemaRefiner : ISchemaRefiner
{
    private readonly IBvLogger logger;
    private readonly SqlConfiguration? configuration;
    private readonly UsedConfigurationTracker? usageTracker;

    public SchemaRefiner(IBvLogger logger, SqlConfiguration? configuration, UsedConfigurationTracker? usageTracker = null)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.usageTracker = usageTracker;
    }

    /// <summary>
    /// Refines the raw database schema into a structured schema, applying SQL type
    /// and nullability overrides from the configuration.
    /// </summary>
    /// <param name="rawDatabaseSchema">The raw database schema from the ingestion phase (Phase 1).</param>
    /// <returns>A refined database schema with complete type information.</returns>
    public DatabaseSchema Refine(RawDatabaseSchema rawDatabaseSchema)
    {
        this.logger.LogMessage("Phase 2: Refining schema model...");

        var databaseSchema = new DatabaseSchema(rawDatabaseSchema.DatabaseName);

        // Process tables
        this.logger.LogMessage("Processing tables...");
        foreach (var tableStatement in rawDatabaseSchema.TableStatements)
        {
            var table = this.ProcessTable(tableStatement, rawDatabaseSchema, this.configuration);
            databaseSchema.AddObject(table);
        }

        // Process views
        this.logger.LogMessage("Processing views...");
        foreach (var viewStatement in rawDatabaseSchema.ViewStatements)
        {
            var view = this.ProcessView(viewStatement, databaseSchema, this.configuration);
            databaseSchema.AddObject(view);
        }

        // Add indexes to tables
        this.logger.LogMessage("Processing indexes...");
        foreach (var indexStatement in rawDatabaseSchema.IndexStatements)
        {
            this.ProcessIndex(indexStatement, databaseSchema);
        }

        this.logger.LogMessage("Phase 2: Schema refinement completed.");
        return databaseSchema;
    }

    private DatabaseObject ProcessTable(CreateTableStatement tableStatement, RawDatabaseSchema rawDatabaseSchema, SqlConfiguration? configuration)
    {
        var schemaName = tableStatement.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo";
        var tableName = tableStatement.SchemaObjectName.BaseIdentifier.Value;

        this.logger.LogMessage($"Processing table: {schemaName}.{tableName}");

        var databaseObject = new DatabaseObject(schemaName, tableName, false);

        // Process columns
        foreach (var column in tableStatement.Definition.ColumnDefinitions)
        {
            var columnName = column.ColumnIdentifier.Value;
            var dataType = this.ExtractDataType(column.DataType);
            var isNullable = column.Constraints.Any(c => c is Microsoft.SqlServer.TransactSql.ScriptDom.NullableConstraintDefinition nc && nc.Nullable);
            var isPrimaryKey = column.Constraints.Any(c => c is UniqueConstraintDefinition uc && uc.IsPrimaryKey);

            // For calculated columns, the data type is not defined. We must parse it from comments.
            var (commentSqlType, commentIsNullable) = this.ParseTypeAnnotationsFromComments(column, tableStatement.ScriptTokenStream);

            // Apply column overrides before creating the column (for Phase 2 responsibilities)
            var (finalDataType, finalIsNullable, sourceInfo) = this.ApplyColumnOverrides(
                commentSqlType ?? dataType,
                commentIsNullable ?? isNullable,
                schemaName,
                tableName,
                columnName,
                configuration);

            // Extract precision/scale for decimal types
            int? maxLength = null, precision = null, scale = null;
            if (column.DataType is SqlDataTypeReference sqlTypeRef)
            {
                if (sqlTypeRef.SqlDataTypeOption == SqlDataTypeOption.Decimal ||
                    sqlTypeRef.SqlDataTypeOption == SqlDataTypeOption.Numeric)
                {
                    var parameters = sqlTypeRef.Parameters;
                    if (parameters.Count >= 2)
                    {
                        if (parameters[0] is Literal p1 && int.TryParse(p1.Value, out var precisionValue))
                        {
                            precision = precisionValue;
                        }

                        if (parameters[1] is Literal p2 && int.TryParse(p2.Value, out var scaleValue))
                        {
                            scale = scaleValue;
                        }
                    }
                }
                else if (sqlTypeRef.Parameters.Count > 0)
                {
                    // Handle max length parameters
                    var param = sqlTypeRef.Parameters[0];
                    if (param is Literal literal &&
                        !string.Equals(literal.Value, "max", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(literal.Value, out var lengthValue))
                    {
                        maxLength = lengthValue;
                    }
                }
            }

            // Create the database column
            var sqlTypeString = finalDataType;
            if (maxLength.HasValue)
            {
                sqlTypeString += $"({maxLength})";
            }
            else if (precision.HasValue && scale.HasValue)
            {
                sqlTypeString += $"({precision},{scale})";
            }
            else if (precision.HasValue)
            {
                sqlTypeString += $"({precision})";
            }

            var databaseType = PwSqlType.Parse(finalDataType);

            var dbColumn = new DatabaseColumn(
                name: columnName,
                databaseType: databaseType,
                isNullable: finalIsNullable,
                isPrimaryKey: isPrimaryKey,
                schema: schemaName,
                tableName: tableName);

            // Set the source info
            dbColumn.SourceInfo = sourceInfo ?? new PropertySourceInfo("SQL Definition", "SQL DDL", $"Defined in CREATE TABLE statement as '{dataType}' {(isNullable ? "NULL" : "NOT NULL")}.");

            databaseObject.Columns.Add(dbColumn);

            // Track primary key columns
            if (isPrimaryKey)
            {
                databaseObject.PrimaryKeyColumns.Add(columnName);
            }
        }

        // Process table constraints to extract primary keys that aren't defined at the column level
        foreach (var constraint in tableStatement.Definition.TableConstraints)
        {
            if (constraint is UniqueConstraintDefinition uniqueConstraint &&
                uniqueConstraint.IsPrimaryKey)
            {
                foreach (var column in uniqueConstraint.Columns)
                {
                    var columnName = column.Column.MultiPartIdentifier.Identifiers.Last().Value;
                    databaseObject.PrimaryKeyColumns.Add(columnName);

                    // Update the column's primary key status
                    var dbColumn = databaseObject.Columns.FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.Ordinal));
                    if (dbColumn != null)
                    {
                        // Since we can't modify the IsPrimaryKey property directly (it's readonly),
                        // we'll need to replace the column with a new instance
                        int index = databaseObject.Columns.IndexOf(dbColumn);
                        if (index >= 0)
                        {
                            databaseObject.Columns[index] = new DatabaseColumn(
                                dbColumn.Name,
                                dbColumn.DatabaseType,
                                dbColumn.IsNullable,
                                true, // Set primary key flag to true
                                dbColumn.Schema,
                                dbColumn.TableName,
                                dbColumn.DatabaseName);
                        }
                    }
                }
            }
        }

        return databaseObject;
    }

    private DatabaseObject ProcessView(CreateViewStatement viewStatement, DatabaseSchema databaseSchema, SqlConfiguration? configuration)
    {
        var schemaName = viewStatement.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo";
        var viewName = viewStatement.SchemaObjectName.BaseIdentifier.Value;

        this.logger.LogMessage($"Processing view: {schemaName}.{viewName}");

        var databaseObject = new DatabaseObject(schemaName, viewName, true);

        if (viewStatement.SelectStatement?.QueryExpression is not QuerySpecification querySpec)
        {
            this.logger.LogMessage($"WARNING: View {schemaName}.{viewName} is not a simple SELECT statement. Columns will be indeterminate.");
            return databaseObject; // Return an empty view object
        }

        var tableAliases = this.ExtractTableAliases(querySpec.FromClause, databaseSchema);

        foreach (var selectElement in querySpec.SelectElements.OfType<SelectScalarExpression>())
        {
            var (columnName, baseColumn) = this.ResolveViewColumn(selectElement, tableAliases, databaseSchema);

            if (string.IsNullOrEmpty(columnName))
            {
                continue;
            }

            var (commentSqlType, commentIsNullable) = this.ParseTypeAnnotationsFromComments(selectElement, viewStatement.ScriptTokenStream);

            // Determine initial source before applying overrides
            PropertySourceInfo initialSourceInfo;
            if (commentSqlType != null || commentIsNullable.HasValue)
            {
                initialSourceInfo = new PropertySourceInfo("SQL Definition", "SQL Comment Annotation", "Type/nullability was specified by a '-- @type' or '-- @nullable' comment.");
            }
            else if (baseColumn?.SourceInfo is PropertySourceInfo bcsi)
            {
                initialSourceInfo = bcsi; // Inherit source info from the base column/function
            }
            else
            {
                initialSourceInfo = new PropertySourceInfo("SQL Definition", "Indeterminate", "Could not determine column source.");
            }

            var originalSqlType = commentSqlType ?? baseColumn?.DatabaseType.ToString() ?? "unknown";
            var originalIsNullable = commentIsNullable ?? baseColumn?.IsNullable ?? true;

            var (finalDataType, finalIsNullable, overrideSourceInfo) = this.ApplyColumnOverrides(
                originalSqlType, originalIsNullable, schemaName, viewName, columnName, configuration);

            var databaseType = string.IsNullOrWhiteSpace(finalDataType) || string.Equals(finalDataType, "unknown",
StringComparison.Ordinal) ? PwSqlType.Unknown
                : PwSqlType.Parse(finalDataType.ToUpper());

            var dbColumn = new DatabaseColumn(
                name: columnName,
                databaseType: databaseType,
                isNullable: finalIsNullable,
                isPrimaryKey: false,
                schema: schemaName,
                tableName: viewName)
            {
                IsIndeterminate = baseColumn == null && commentSqlType == null,

                // Use the override source if it exists, otherwise use the one we determined
                SourceInfo = overrideSourceInfo ?? initialSourceInfo,
            };

            databaseObject.Columns.Add(dbColumn);
        }

        return databaseObject;
    }

    private (string? SqlType, bool? IsNullable) ParseTypeAnnotationsFromComments(TSqlFragment fragment, IList<TSqlParserToken> tokens)
    {
        string? sqlType = null;
        bool? isNullable = null;

        // Find the token index for the start of the current fragment.
        int fragmentStartIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Offset >= fragment.StartOffset)
            {
                fragmentStartIndex = i;
                break;
            }
        }

        if (fragmentStartIndex == -1)
        {
            return (null, null);
        }

        // Look backwards from the fragment's start token for immediately preceding comments.
        for (int i = fragmentStartIndex - 1; i >= 0; i--)
        {
            var token = tokens[i];

            // Ignore whitespace, which can appear as separate tokens.
            if (token.TokenType == TSqlTokenType.WhiteSpace)
            {
                continue;
            }

            // If we hit a comma or a keyword that starts a new definition, we've gone too far back.
            if (token.TokenType == TSqlTokenType.Comma ||
                (token.TokenType == TSqlTokenType.Identifier &&
                 (token.Text.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ||
                  token.Text.Equals("CREATE", StringComparison.OrdinalIgnoreCase))))
            {
                break;
            }

            if (token.TokenType == TSqlTokenType.SingleLineComment)
            {
                var commentText = token.Text.Trim('-', ' ');
                if (commentText.StartsWith("@type:", StringComparison.OrdinalIgnoreCase))
                {
                    // Only assign if not already found. This ensures the comment
                    // closest to the column takes precedence if multiple are present.
                    if (sqlType == null)
                    {
                        sqlType = commentText.Substring("@type:".Length).Trim();
                    }
                }
                else if (commentText.StartsWith("@nullable:", StringComparison.OrdinalIgnoreCase))
                {
                    if (isNullable == null && bool.TryParse(commentText.Substring("@nullable:".Length).Trim(), out var parsedNullable))
                    {
                        isNullable = parsedNullable;
                    }
                }
            }
            else
            {
                // If we encounter any other token that is not a comment or whitespace,
                // it means we've moved past the relevant comment block for this column.
                break;
            }
        }

        return (sqlType, isNullable);
    }

    internal Dictionary<string, DatabaseObject> ExtractTableAliases(FromClause? fromClause, DatabaseSchema databaseSchema)
    {
        var aliases = new Dictionary<string, DatabaseObject>(StringComparer.OrdinalIgnoreCase);
        if (fromClause == null)
        {
            return aliases;
        }

        foreach (var tableReference in fromClause.TableReferences)
        {
            this.ProcessTableReference(tableReference, aliases, databaseSchema);
        }

        return aliases;
    }

    private void ProcessTableReference(TableReference tableReference, Dictionary<string, DatabaseObject> aliases, DatabaseSchema databaseSchema)
    {
        if (tableReference is NamedTableReference namedRef)
        {
            var schema = namedRef.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
            var table = namedRef.SchemaObject.BaseIdentifier.Value;
            var fullTableName = $"{schema}.{table}";

            if (databaseSchema.ObjectsByName.TryGetValue(fullTableName, out var dbObject))
            {
                var alias = namedRef.Alias?.Value ?? table;
                aliases[alias] = dbObject;
            }
        }
        else if (tableReference is QualifiedJoin join)
        {
            this.ProcessTableReference(join.FirstTableReference, aliases, databaseSchema);
            this.ProcessTableReference(join.SecondTableReference, aliases, databaseSchema);
        }
        else if (tableReference is UnqualifiedJoin ujoin)
        {
            this.ProcessTableReference(ujoin.FirstTableReference, aliases, databaseSchema);
            this.ProcessTableReference(ujoin.SecondTableReference, aliases, databaseSchema);
        }

        // else if (tableReference is Microsoft.SqlServer.TransactSql.ScriptDom ApplyExpression apply)
        // {
        //    // An APPLY clause was found. We don't need to resolve the function call itself,
        //    // but we must process its input to find the tables that came before it (e.g., the base table and its joins).
        //    ProcessTableReference(apply.Input, aliases, databaseSchema);
        // }
        else if (tableReference is QueryDerivedTable derivedTable)
        {
            // This handles subqueries in the FROM clause, e.g., FROM (SELECT...) AS alias
            var subqueryProcessor = new SubqueryProcessor(this.logger, databaseSchema, this);
            var virtualTable = subqueryProcessor.Process(derivedTable);
            if (virtualTable != null)
            {
                aliases[virtualTable.Name] = virtualTable;
            }
        }
    }

    internal (string? ColumnName, DatabaseColumn? BaseColumn) ResolveViewColumn(SelectScalarExpression selectElement, Dictionary<string, DatabaseObject> tableAliases, DatabaseSchema databaseSchema)
    {
        string? columnName = selectElement.ColumnName?.Value;

        // Case 1: Direct column reference (e.g., SELECT t.Id FROM Table t)
        if (selectElement.Expression is ColumnReferenceExpression colRef)
        {
            var identifiers = colRef.MultiPartIdentifier.Identifiers;
            var colName = identifiers.Last().Value;
            columnName ??= colName;

            string? tableAlias = identifiers.Count > 1 ? identifiers.First().Value : null;

            DatabaseObject? sourceTable = null;
            if (tableAlias != null)
            {
                tableAliases.TryGetValue(tableAlias, out sourceTable);
            }
            else if (tableAliases.Count == 1)
            {
                sourceTable = tableAliases.Values.First();
            }

            if (sourceTable != null)
            {
                var baseColumn = sourceTable.Columns.FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (baseColumn != null)
                {
                    return (columnName, baseColumn);
                }
            }
        }

        // Case 2: Function call (CONCAT, ISNULL, aggregates, etc.)
        else if (selectElement.Expression is FunctionCall funcCall)
        {
            var functionName = funcCall.FunctionName.Value;
            columnName ??= $"UNKNOWN_COLUMN_{selectElement.StartOffset}";

            // Handle ISNULL specifically
            if (functionName.Equals("ISNULL", StringComparison.OrdinalIgnoreCase) && funcCall.Parameters.FirstOrDefault() is ColumnReferenceExpression firstParam)
            {
                // Try to resolve the type from the first parameter of ISNULL
                var (_, baseColumn) = this.ResolveViewColumn(new SelectScalarExpression { Expression = firstParam }, tableAliases, databaseSchema);
                if (baseColumn != null)
                {
                    // The result of ISNULL is the type of the first arg, but it's NOT NULL.
                    var pseudoColumn = new DatabaseColumn(columnName, baseColumn.DatabaseType, false, false, string.Empty, string.Empty)
                    {
                        SourceInfo = new PropertySourceInfo("SQL Definition", "Function Inference", $"Inferred from ISNULL({baseColumn.Name}). Type is based on '{baseColumn.Name}' but is non-nullable."),
                    };
                    return (columnName, pseudoColumn);
                }
            }

            // Handle CONCAT specifically
            else if (functionName.Equals("CONCAT", StringComparison.OrdinalIgnoreCase))
            {
                var pseudoColumn = new DatabaseColumn(columnName, PwSqlType.Parse("NVARCHAR(MAX)"), false, false, string.Empty, string.Empty)
                {
                    SourceInfo = new PropertySourceInfo("SQL Definition", "Function Inference", "Inferred from CONCAT function call, resulting in a non-nullable string."),
                };
                return (columnName, pseudoColumn);
            }

            // Handle common aggregate functions
            var aggregateType = this.GetAggregateSqlType(functionName);
            if (aggregateType != null)
            {
                // For aggregates like SUM, we might try to infer the type from the parameter, but for now, a default is robust.
                var pseudoColumn = new DatabaseColumn(columnName, PwSqlType.Parse(aggregateType), false, false, string.Empty, string.Empty)
                {
                    SourceInfo = new PropertySourceInfo("SQL Definition", "Function Inference", $"Inferred from aggregate function '{functionName.ToUpper()}', resulting in a non-nullable '{aggregateType}'."),
                };
                return (columnName, pseudoColumn);
            }
        }

        // Case 3: String literal value
        else if (selectElement.Expression is StringLiteral)
        {
            columnName ??= $"UNKNOWN_COLUMN_{selectElement.StartOffset}";
            var pseudoColumn = new DatabaseColumn(columnName, PwSqlType.Parse("NVARCHAR(MAX)"), false, false, string.Empty, string.Empty)
            {
                SourceInfo = new PropertySourceInfo("SQL Definition", "Literal Inference", "Inferred from a string literal value, resulting in a non-nullable string."),
            };
            return (columnName, pseudoColumn);
        }

        // Case 4: All other expressions (other literals, CASE statements, etc.)
        columnName ??= $"UNKNOWN_COLUMN_{selectElement.StartOffset}";
        return (columnName, null);
    }

    private string? GetAggregateSqlType(string functionName)
    {
        return functionName.ToUpperInvariant() switch
        {
            "COUNT" or "COUNT_BIG" => "INT",
            "SUM" or "AVG" => "DECIMAL(38, 6)", // A safe default for sums/averages
            "MIN" or "MAX" => "SQL_VARIANT", // Could be any type, variant is a placeholder
            _ => null
        };
    }

    private void ProcessIndex(CreateIndexStatement indexStatement, DatabaseSchema databaseSchema)
    {
        var schemaName = indexStatement.OnName.SchemaIdentifier?.Value ?? "dbo";
        var tableName = indexStatement.OnName.BaseIdentifier.Value;
        var indexName = indexStatement.Name.Value;
        var isUnique = indexStatement.Unique;

        this.logger.LogMessage($"Processing index: {indexName} on {schemaName}.{tableName}");

        // Find the table in the schema
        var fullTableName = $"{schemaName}.{tableName}";
        if (!databaseSchema.ObjectsByName.TryGetValue(fullTableName, out var databaseObject) || databaseObject.IsView)
        {
            this.logger.LogMessage($"WARNING: Could not find table {fullTableName} for index {indexName}");
            return;
        }

        // Create the index definition
        var indexDefinition = new Model.IndexDefinition(indexName, isUnique, false); // false for non-clustered by default

        // Extract columns
        foreach (var column in indexStatement.Columns)
        {
            var columnName = column.Column.MultiPartIdentifier.Identifiers.Last().Value;
            indexDefinition.ColumnNames.Add(columnName);

            // Also track the index in the column's participating indexes
            var dbColumn = databaseObject.Columns.FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.Ordinal));
            if (dbColumn != null)
            {
                dbColumn.IndexNames.Add(indexName);
            }
        }

        // Add the index to the table
        databaseObject.Indexes.Add(indexDefinition);
    }

    private (string dataType, bool isNullable, PropertySourceInfo? sourceInfo) ApplyColumnOverrides(
        string originalDataType,
        bool originalIsNullable,
        string schema,
        string objectName,
        string columnName,
        SqlConfiguration? configuration)
    {
        var tableKey = $"{schema}.{objectName}";
        var columnOverride = configuration?.Tables?.GetValueOrDefault(tableKey)?.ColumnOverrides?.GetValueOrDefault(columnName);

        var finalDataType = originalDataType;
        var finalIsNullable = originalIsNullable;
        var details = new List<string>();

        if (columnOverride != null)
        {
            if (!string.IsNullOrWhiteSpace(columnOverride.SqlType))
            {
                finalDataType = columnOverride.SqlType;
                details.Add($"SQL type set to '{finalDataType}'");
                this.usageTracker?.MarkColumnOverrideUsed(tableKey, columnName, nameof(columnOverride.SqlType));
            }

            if (columnOverride.IsNullable.HasValue)
            {
                finalIsNullable = columnOverride.IsNullable.Value;
                details.Add($"nullability set to '{finalIsNullable}'");
                this.usageTracker?.MarkColumnOverrideUsed(tableKey, columnName, nameof(columnOverride.IsNullable));
            }
        }

        if (details.Count == 0)
        {
            return (originalDataType, originalIsNullable, null);
        }

        var sourceInfo = new PropertySourceInfo(
            "SQL Definition",
            "Column Override",
            $"{string.Join(" and ", details)} by configuration for '{schema}.{objectName}.{columnName}'.");

        return (finalDataType, finalIsNullable, sourceInfo);
    }

    private string ExtractDataType(DataTypeReference dataType)
    {
        if (dataType is SqlDataTypeReference sqlType)
        {
            return sqlType.SqlDataTypeOption.ToString().ToLowerInvariant();
        }
        else if (dataType is UserDataTypeReference userType)
        {
            return userType.Name.BaseIdentifier.Value;
        }

        return "unknown";
    }

    // Removed GetSqlCoreType method - Type mapping is the responsibility of Phase 3 (CSharpModelTransformer)
    // The SchemaRefiner (Phase 2) is only responsible for handling raw SQL type strings
    private string[] ExtractViewColumnNames(CreateViewStatement viewStatement)
    {
        // Try to extract column names from the view definition
        if (viewStatement.SelectStatement == null)
        {
            this.logger.LogMessage($"WARNING: View {viewStatement.SchemaObjectName} has no select statement");
            return Array.Empty<string>();
        }

        // Determine if we have a direct QuerySpecification or need to extract it from SelectStatement
        QuerySpecification? querySpec = null;

        // Try to get the QuerySpecification from various possible locations in the syntax tree
        if (viewStatement.SelectStatement.QueryExpression is QuerySpecification directQuerySpec)
        {
            querySpec = directQuerySpec;
        }
        else if (viewStatement.SelectStatement.QueryExpression is BinaryQueryExpression binaryQuery)
        {
            // Handle UNION, EXCEPT, INTERSECT - we'll use the first part of the query for column names
            if (binaryQuery.FirstQueryExpression is QuerySpecification firstQuery)
            {
                querySpec = firstQuery;
                this.logger.LogMessage($"WARNING: View {viewStatement.SchemaObjectName} uses a binary operation (UNION/EXCEPT/INTERSECT). Only extracting columns from first part.");
            }
        }

        // Extract column names from the query specification
        if (querySpec?.SelectElements != null)
        {
            return querySpec.SelectElements
                .OfType<SelectScalarExpression>()
                .Select(e => e.ColumnName?.Value ??
                             (e.Expression is ColumnReferenceExpression colRef
                                ? colRef.MultiPartIdentifier.Identifiers.Last().Value
                                : $"Column{querySpec.SelectElements.IndexOf(e) + 1}"))
                .ToArray();
        }

        // Fallback - if we can't extract column names, return an empty array
        this.logger.LogMessage($"WARNING: Could not extract column names from view {viewStatement.SchemaObjectName}");
        return Array.Empty<string>();
    }
}
}
