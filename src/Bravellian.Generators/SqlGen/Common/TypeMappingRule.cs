// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// Represents a rule for mapping SQL column types to custom C# types.
/// </summary>
public class TypeMappingRule
{
    /// <summary>
    /// Gets or sets the pattern to match column names.
    /// </summary>
    public string ColumnNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern to match C# types.
    /// </summary>
    public string CSharpTypePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern to match table names.
    /// </summary>
    public string TableNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern to match schema names.
    /// </summary>
    public string SchemaNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern to match database names.
    /// </summary>
    public string DatabaseNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target C# type.
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the target type should be nullable.
    /// Null means use the default nullability from the database schema.
    /// </summary>
    public bool? IsNullable { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the column name pattern is a regex pattern.
    /// </summary>
    public bool IsRegexPattern { get; set; } = false;

    /// <summary>
    /// Determines whether this rule matches the given column name and C# type.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="csharpType">The C# type of the column.</param>
    /// <returns>True if the rule matches, false otherwise.</returns>
    public bool MatchesColumnAndType(string schemaName, string tableName, string columnName, string csharpType)
    {
        // If table name pattern is specified, check if it matches
        if (!string.IsNullOrEmpty(this.TableNamePattern))
        {
            var tableMatch = string.Equals(tableName, this.TableNamePattern, StringComparison.OrdinalIgnoreCase);
            if (!tableMatch)
            {
                return false;
            }
        }

        // If schema name pattern is specified, check if it matches
        if (!string.IsNullOrEmpty(this.SchemaNamePattern))
        {
            var schemaMatch = string.Equals(schemaName, this.SchemaNamePattern, StringComparison.OrdinalIgnoreCase);
            if (!schemaMatch)
            {
                return false;
            }
        }

        // If the C# type pattern is empty, consider it a match for any type
        // This is especially useful for view columns where the type might not be correctly identified
        var typeMatch = string.IsNullOrEmpty(this.CSharpTypePattern);
        if (!typeMatch)
        {
            // If not empty, check if it matches the column's type
            typeMatch = string.Equals(csharpType, "UNKNOWN_TYPE_ERROR", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(csharpType, this.CSharpTypePattern, StringComparison.OrdinalIgnoreCase);

            // If still not a match, try regex matching
            if (!typeMatch)
            {
                var csharpTypeRegex = new Regex($"^{this.CSharpTypePattern}$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                typeMatch = csharpTypeRegex.IsMatch(csharpType);
            }
        }

        // Check if the column name pattern is a simple string or a regex pattern
        bool columnMatch;
        if (!this.IsRegexPattern && !this.ContainsRegexSpecialCharacters(this.ColumnNamePattern))
        {
            columnMatch = string.Equals(columnName, this.ColumnNamePattern, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var columnNameRegex = new Regex(this.ColumnNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            columnMatch = columnNameRegex.IsMatch(columnName);
        }

        var result = columnMatch && typeMatch;

        return result;
    }

    /// <summary>
    /// Determines whether this rule matches the given table name and column name.
    /// </summary>
    /// <param name="tableName">The name of the table or view.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>True if the rule matches, false otherwise.</returns>
    public bool MatchesTableAndColumn(string schemaName, string tableName, string columnName)
    {
        // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"MatchesTableAndColumn called with tableName={tableName}, columnName={columnName}");
        // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"Rule: DB={this.DatabaseNamePattern}, Schema={this.SchemaNamePattern}, Table={this.TableNamePattern}, Column={this.ColumnNamePattern}, Type={this.TargetType}");

        // If database name pattern is specified, this rule requires database name matching
        if (!string.IsNullOrEmpty(this.DatabaseNamePattern))
        {
            // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"Rule skipped because DatabaseNamePattern is specified");
            return false;
        }

        // If table name pattern is not specified, this rule matches any table
        // but still needs to match the column
        if (string.IsNullOrEmpty(this.TableNamePattern))
        {
            // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"TableNamePattern is empty, treating as wildcard");

            // Check if the column name pattern is a simple string or a regex pattern
            bool columnOnlyMatch;
            if (!this.IsRegexPattern && !this.ContainsRegexSpecialCharacters(this.ColumnNamePattern))
            {
                columnOnlyMatch = string.Equals(columnName, this.ColumnNamePattern, StringComparison.OrdinalIgnoreCase);

                // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"Column exact match: {columnOnlyMatch}");
            }
            else
            {
                var columnNameRegex = new Regex(this.ColumnNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                columnOnlyMatch = columnNameRegex.IsMatch(columnName);
            }

            // Check if a schema pattern is specified - if so, we can't match it here
            if (!string.IsNullOrEmpty(this.SchemaNamePattern))
            {
                return false;
            }

            // this.debugLogger.LogDebug(schemaName, tableName, columnName, $"MatchesTableAndColumn result for column-only rule: {columnOnlyMatch}");

            return columnOnlyMatch;
        }

        bool tableMatch;
        bool columnMatch;
        var schemaMatch = true; // Default to true if no schema pattern is specified

        // Check if the table name pattern is a simple string (no regex special characters)
        if (!this.ContainsRegexSpecialCharacters(this.TableNamePattern))
        {
            tableMatch = string.Equals(tableName, this.TableNamePattern, StringComparison.OrdinalIgnoreCase);

            // this.debugLogger.LogDebug(schemaName, this.TableNamePattern, this.ColumnNamePattern, $"Table exact match: {tableMatch}");
        }
        else
        {
            var tableNameRegex = new Regex(this.TableNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            tableMatch = tableNameRegex.IsMatch(tableName);
        }

        // Check if a schema pattern is specified
        if (!string.IsNullOrEmpty(this.SchemaNamePattern))
        {
            // For now, we don't have schema information in this method, so we can't match it
            // This will be handled in the MatchesDatabaseTableAndColumn method
            schemaMatch = false;

            // this.debugLogger.LogDebug(schemaName, this.TableNamePattern, this.ColumnNamePattern, $"Schema match set to false because SchemaNamePattern is specified");
        }

        // Check if the column name pattern is a simple string or a regex pattern
        if (!this.IsRegexPattern && !this.ContainsRegexSpecialCharacters(this.ColumnNamePattern))
        {
            columnMatch = string.Equals(columnName, this.ColumnNamePattern, StringComparison.OrdinalIgnoreCase);

            // this.debugLogger.LogDebug(schemaName, this.TableNamePattern, this.ColumnNamePattern, $"Column exact match: {columnMatch}");
        }
        else
        {
            var columnNameRegex = new Regex(this.ColumnNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            columnMatch = columnNameRegex.IsMatch(columnName);
        }

        var result = tableMatch && columnMatch && schemaMatch;

        return result;
    }

    /// <summary>
    /// Determines whether this rule matches the given database name, table name, and column name.
    /// </summary>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="tableName">The name of the table or view.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>True if the rule matches, false otherwise.</returns>
    public bool MatchesDatabaseTableAndColumn(string schemaName, string databaseName, string tableName, string columnName)
    {
        // If database name pattern is not specified, this rule doesn't match database names
        if (string.IsNullOrEmpty(this.DatabaseNamePattern))
        {
            return false;
        }

        // If table name pattern is not specified, this rule doesn't match table names
        if (string.IsNullOrEmpty(this.TableNamePattern))
        {
            return false;
        }

        bool databaseMatch;
        bool tableMatch;
        bool columnMatch;

        // Check if the database name pattern is a simple string (no regex special characters)
        if (!this.ContainsRegexSpecialCharacters(this.DatabaseNamePattern))
        {
            databaseMatch = string.Equals(databaseName, this.DatabaseNamePattern, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var databaseNameRegex = new Regex(this.DatabaseNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            databaseMatch = databaseNameRegex.IsMatch(databaseName);
        }

        // Check if the table name pattern is a simple string (no regex special characters)
        if (!this.ContainsRegexSpecialCharacters(this.TableNamePattern))
        {
            tableMatch = string.Equals(tableName, this.TableNamePattern, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var tableNameRegex = new Regex(this.TableNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            tableMatch = tableNameRegex.IsMatch(tableName);
        }

        // Check if the column name pattern is a simple string or a regex pattern
        if (!this.IsRegexPattern && !this.ContainsRegexSpecialCharacters(this.ColumnNamePattern))
        {
            columnMatch = string.Equals(columnName, this.ColumnNamePattern, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var columnNameRegex = new Regex(this.ColumnNamePattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            columnMatch = columnNameRegex.IsMatch(columnName);
        }

        var result = databaseMatch && tableMatch && columnMatch;

        return result;
    }

    /// <summary>
    /// Determines if a string contains regex special characters.
    /// </summary>
    /// <param name="pattern">The pattern to check.</param>
    /// <returns>True if the pattern contains regex special characters, false otherwise.</returns>
    private bool ContainsRegexSpecialCharacters(string pattern)
    {
        // Check if the pattern contains any regex special characters
        return pattern.IndexOfAny(new[] { '*', '+', '?', '^', '$', '.', '(', ')', '[', ']', '{', '}', '|', '\\' }) >= 0;
    }

    /// <summary>
    /// Gets the target type for a specific column name.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The target type.</returns>
    public string GetTargetType(string columnName)
    {
        // Console.WriteLine($"DEBUG: GetTargetType called for column {columnName} with rule {this.ColumnNamePattern}:{this.CSharpTypePattern}={this.TargetType}");

        // Handle special target types
        if (string.Equals(this.TargetType, "special:identifier", StringComparison.OrdinalIgnoreCase))
        {
            // For identifier types, use the column name + "entifier"
            var result = $"Bravellian.Core.{columnName}entifier";
            return result;
        }
        else if (string.Equals(this.TargetType, "special:erpidentifier", StringComparison.OrdinalIgnoreCase))
        {
            // For ERP identifier types, use the table name + "ErpIdentifier"
            // This is handled specially in GetSpecializedType since we need the actual table name
            return "special:erpidentifier";
        }
        else if (string.Equals(this.TargetType, "special:columnerpidentifier", StringComparison.OrdinalIgnoreCase))
        {
            // For column-based ERP identifier types, use the column name + "entifier"
            var result = $"Bravellian.{columnName}entifier";
            return result;
        }

        // Console.WriteLine($"DEBUG: Returning target type {this.TargetType} for column {columnName}");
        return this.TargetType;
    }
}

