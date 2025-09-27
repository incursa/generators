// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline;

using System.Collections.Generic;

/// <summary>
/// Represents a SQL column definition.
/// </summary>
public class PwColumnDefinition(
    string name,
    string csharpType,
    bool isNullable,
    bool isPrimaryKey,
    List<string>? parameters = null,
    string? originalSqlType = null)
{
    public string Name { get; } = name;

    public string CSharpType { get; } = csharpType;

    public bool IsNullable { get; } = isNullable;

    public bool IsPrimaryKey { get; } = isPrimaryKey;

    public List<string>? Parameters { get; } = parameters;

    /// <summary>
    /// Gets the original SQL type of the column (e.g., VARCHAR, NVARCHAR).
    /// </summary>
    public string? OriginalSqlType { get; } = originalSqlType;
}

