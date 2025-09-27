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

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline;

using System;
using System.Collections.Generic;

/// <summary>
/// Configuration for mapping SQL column types to custom C# types.
/// </summary>
public class TypeMappingConfiguration
{
    private readonly List<TypeMappingRule> rules =[];

    public TypeMappingConfiguration()
    {
        this.AddDefaultRules();
    }

    public TypeMappingConfiguration(bool addDefaultRules)
    {
        if (addDefaultRules)
        {
            this.AddDefaultRules();
        }
    }

    /// <summary>
    /// Adds a type mapping rule.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    public void AddRule(TypeMappingRule rule)
    {
        // Add the rule at the beginning of the list to give it higher priority than default rules
        this.rules.Insert(0, rule);

        // Console.WriteLine($"DEBUG: Added rule #{this.rules.Count}: {rule.ColumnNamePattern}:{rule.CSharpTypePattern}={rule.TargetType} (TablePattern: '{rule.TableNamePattern}', DatabasePattern: '{rule.DatabaseNamePattern}')");
    }

    /// <summary>
    /// Gets the mapped type for a column based on its name and C# type.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="csharpType">The C# type of the column.</param>
    /// <returns>The mapped type, or null if no mapping is found.</returns>
    public string? GetMappedType(string? schemaName, string? tableName, string? columnName, string csharpType)
    {
        var ruleIndex = 0;
        foreach (TypeMappingRule rule in this.rules)
        {
            ruleIndex++;

            if (rule.MatchesColumnAndType(schemaName!, tableName!, columnName!, csharpType))
            {
                return rule.GetTargetType(columnName);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the mapped type for a column based on its table name and column name.
    /// </summary>
    /// <param name="tableName">The name of the table or view.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="csharpType">The C# type of the column.</param>
    /// <param name="databaseName">Optional database name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <returns>The mapped type, or null if no mapping is found.</returns>
    public string? GetMappedType(string tableName, string columnName, string csharpType, string? databaseName = null, string? schemaName = null)
    {
        // First try to match based on database, schema, table, and column
        if (databaseName != null && schemaName != null)
        {
            var ruleIndex = 0;
            foreach (TypeMappingRule rule in this.rules)
            {
                ruleIndex++;

                if (!string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    rule.MatchesDatabaseTableAndColumn(schemaName, databaseName, tableName, columnName))
                {
                    // Check if the schema matches
                    var schemaMatch = string.Equals(schemaName, rule.SchemaNamePattern, StringComparison.OrdinalIgnoreCase);

                    if (schemaMatch)
                    {
                        return rule.GetTargetType(columnName);
                    }
                }
            }
        }

        // Then try to match based on database, table, and column (without schema)
        if (databaseName != null)
        {
            foreach (TypeMappingRule rule in this.rules)
            {
                if (string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    rule.MatchesDatabaseTableAndColumn(schemaName!, databaseName, tableName, columnName))
                {
                    return rule.GetTargetType(columnName);
                }
            }
        }

        // Then try to match based on schema, table, and column (without database)
        if (schemaName != null)
        {
            foreach (TypeMappingRule rule in this.rules)
            {
                if (!string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    string.IsNullOrEmpty(rule.DatabaseNamePattern) &&
                    rule.MatchesTableAndColumn(schemaName, tableName, columnName))
                {
                    // Check if the schema matches
                    var schemaMatch = string.Equals(schemaName, rule.SchemaNamePattern, StringComparison.OrdinalIgnoreCase);
                    if (schemaMatch)
                    {
                        return rule.GetTargetType(columnName);
                    }
                }
            }
        }

        // Then try to match based on table and column (without database and schema)
        foreach (TypeMappingRule rule in this.rules)
        {
            if (string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                string.IsNullOrEmpty(rule.DatabaseNamePattern) &&
                rule.MatchesTableAndColumn(schemaName!, tableName, columnName))
            {
                return rule.GetTargetType(columnName);
            }
        }

        // Fall back to the original column name and type matching
        return this.GetMappedType(schemaName, tableName, columnName, csharpType);
    }

    /// <summary>
    /// Adds default type mapping rules.
    /// </summary>
    public void AddDefaultRules()
    {
        // Add default rules for common patterns

        // // Identifier types
        // this.rules.Add(new TypeMappingRule
        // {
        //     ColumnNamePattern = ".*Id$",
        //     CSharpTypePattern = "Guid",
        //     TargetType = "special:identifier", // Special handling in TypeMappingRule.GetTargetType
        // });

        // // ERP Identifier types
        // this.rules.Add(new TypeMappingRule
        // {
        //     ColumnNamePattern = "^ErpId$",
        //     CSharpTypePattern = "string",
        //     TargetType = "special:erpidentifier", // Special handling in TypeMappingRule.GetTargetType
        // });

        // this.rules.Add(new TypeMappingRule
        // {
        //     ColumnNamePattern = ".*ErpId$",
        //     CSharpTypePattern = "string",
        //     TargetType = "special:columnerpidentifier", // Special handling in TypeMappingRule.GetTargetType
        // });
    }

    /// <summary>
    /// Gets the mapped type for a column based on its name and C# type, including nullability.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="csharpType">The C# type of the column.</param>
    /// <returns>The mapped type and nullability, or null if no mapping is found.</returns>
    public (string? Type, bool? IsNullable) GetMappedTypeWithNullability(string? schemaName, string? tableName, string columnName, string csharpType)
    {
        // Console.WriteLine($"DEBUG: Checking {this.rules.Count} rules for column {columnName} (type {csharpType}) with nullability");
        var ruleIndex = 0;
        foreach (TypeMappingRule rule in this.rules)
        {
            ruleIndex++;

            // Console.WriteLine($"DEBUG: Checking rule #{ruleIndex}: {rule.ColumnNamePattern}:{rule.CSharpTypePattern}={rule.TargetType}");
            if (rule.MatchesColumnAndType(schemaName!, tableName!, columnName, csharpType))
            {
                // Console.WriteLine($"DEBUG: Rule #{ruleIndex} matched for column {columnName} with nullability {rule.IsNullable}");
                return (rule.GetTargetType(columnName), rule.IsNullable);
            }
        }

        // Console.WriteLine($"DEBUG: No rule matched for column {columnName} with nullability");
        return (null, null);
    }

    /// <summary>
    /// Gets the mapped type for a column based on its table name and column name, including nullability.
    /// </summary>
    /// <param name="tableName">The name of the table or view.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="csharpType">The C# type of the column.</param>
    /// <param name="databaseName">Optional database name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <returns>The mapped type and nullability, or null if no mapping is found.</returns>
    public (string? Type, bool? IsNullable) GetMappedTypeWithNullability(string tableName, string columnName, string csharpType, string? databaseName = null, string? schemaName = null)
    {
        // First try to match based on database, schema, table, and column
        if (databaseName != null && schemaName != null)
        {
            foreach (TypeMappingRule rule in this.rules)
            {
                if (!string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    rule.MatchesDatabaseTableAndColumn(schemaName, databaseName, tableName, columnName))
                {
                    // Check if the schema matches
                    var schemaMatch = string.Equals(schemaName, rule.SchemaNamePattern, StringComparison.OrdinalIgnoreCase);
                    if (schemaMatch)
                    {
                        return (rule.GetTargetType(columnName), rule.IsNullable);
                    }
                }
            }
        }

        // Then try to match based on database, table, and column (without schema)
        if (databaseName != null)
        {
            foreach (TypeMappingRule rule in this.rules)
            {
                if (string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    rule.MatchesDatabaseTableAndColumn(schemaName!, databaseName, tableName, columnName))
                {
                    return (rule.GetTargetType(columnName), rule.IsNullable);
                }
            }
        }

        // Then try to match based on schema, table, and column (without database)
        if (schemaName != null)
        {
            foreach (TypeMappingRule rule in this.rules)
            {
                if (!string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                    string.IsNullOrEmpty(rule.DatabaseNamePattern) &&
                    rule.MatchesTableAndColumn(schemaName, tableName, columnName))
                {
                    // Check if the schema matches
                    var schemaMatch = string.Equals(schemaName, rule.SchemaNamePattern, StringComparison.OrdinalIgnoreCase);
                    if (schemaMatch)
                    {
                        return (rule.GetTargetType(columnName), rule.IsNullable);
                    }
                }
            }
        }

        // Then try to match based on table and column (without database and schema)
        foreach (TypeMappingRule rule in this.rules)
        {
            if (string.IsNullOrEmpty(rule.SchemaNamePattern) &&
                string.IsNullOrEmpty(rule.DatabaseNamePattern) &&
                rule.MatchesTableAndColumn(schemaName!, tableName, columnName))
            {
                return (rule.GetTargetType(columnName), rule.IsNullable);
            }
        }

        // Fall back to the original column name and type matching
        return this.GetMappedTypeWithNullability(schemaName, tableName, columnName, csharpType);
    }
}
