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

namespace Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bravellian;
using Bravellian.Generators.SqlGen.Common;

/// <summary>
/// Represents a column in a database object.
/// </summary>
[DebuggerDisplay("{Name} ({DatabaseType}) {(IsNullable ? \"NULL\" : \"NOT NULL\")} {(IsPrimaryKey ? \"PK\" : \"\")}")]
public class DatabaseColumn
{
    /// <summary>
    /// Gets the name of the column.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the database type of the column.
    /// </summary>
    public PwSqlType DatabaseType { get; }

    /// <summary>
    /// Gets the core SQL type of the column.
    /// </summary>
    public SqlCoreType CoreType { get; }

    /// <summary>
    /// Gets the parameters for the SQL type.
    /// </summary>
    public SqlTypeParameters? TypeParameters { get; }

    /// <summary>
    /// Gets a value indicating whether this column is nullable.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets a value indicating whether this column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; }

    /// <summary>
    /// Gets the original SQL type of the column.
    /// </summary>
    public PwSqlType? OriginalSqlType { get; }
    
    /// <summary>
    /// Gets the names of indexes this column participates in.
    /// </summary>
    public HashSet<string> IndexNames { get; } = [];

    /// <summary>
    /// Gets the schema name.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Gets the table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string? DatabaseName { get; }

    /// <summary>
    /// Gets or sets whether the column type could not be definitively determined.
    /// </summary>
    public bool IsIndeterminate { get; set; }

    /// <summary>
    /// Gets or sets whether the column type was overridden by a comment.
    /// </summary>
    public bool IsTypeOverridden { get; set; }

    /// <summary>
    /// Gets or sets the source information for the SQL type and nullability.
    /// </summary>
    public object? SourceInfo { get; set; }


    /// <summary>
    /// Gets or sets the overridden property name from a comment.
    /// </summary>
    public string? PropertyNameOverride { get; set; }

    /// <summary>
    /// Creates a new database column.
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <param name="databaseType">The SQL type.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Whether the column is a primary key.</param>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="originalSqlType">The original SQL type.</param>
    /// <param name="parameters">Optional parameters for the SQL type.</param>
    public DatabaseColumn(
        string name,
        PwSqlType databaseType,
        bool isNullable,
        bool isPrimaryKey,
        string schema,
        string tableName,
        string? databaseName = default,
        PwSqlType? originalSqlType = default,
        List<string>? parameters = default)
    {
        this.Name = name;
        this.DatabaseType = databaseType;
        this.IsNullable = isNullable;
        this.IsPrimaryKey = isPrimaryKey;
        this.Schema = schema;
        this.TableName = tableName;
        this.DatabaseName = databaseName;
        this.OriginalSqlType = originalSqlType ?? databaseType;
        this.IsIndeterminate = false;
        this.IsTypeOverridden = false;

        // Parse the database type to determine the core type and parameters
        var (coreType, typeParams) = this.ParseSqlType(databaseType.Value);
        this.CoreType = coreType;

        // If we have parameters from the SQL parser, use those instead of trying to parse them again
        if (parameters != null && parameters.Count > 0)
        {
            this.TypeParameters = this.ParseTypeParameters(coreType, parameters);
        }
        else
        {
            this.TypeParameters = typeParams;
        }
    }

    private (SqlCoreType CoreType, SqlTypeParameters? TypeParameters) ParseSqlType(string sqlType)
    {
        // Extract base type and parameters
        var parts = sqlType.Split(['('], 2);
        var baseType = parts[0].Trim().ToUpperInvariant();
        var parameters = parts.Length > 1 ? parts[1].TrimEnd(')').Split(',') : Array.Empty<string>();

        // Map base type to core type
        var coreType = baseType switch
        {
            "INT" => SqlCoreType.Int,
            "BIGINT" => SqlCoreType.BigInt,
            "SMALLINT" => SqlCoreType.SmallInt,
            "TINYINT" => SqlCoreType.TinyInt,
            "BIT" => SqlCoreType.Bit,
            "DECIMAL" => SqlCoreType.Decimal,
            "NUMERIC" => SqlCoreType.Numeric,
            "MONEY" => SqlCoreType.Money,
            "SMALLMONEY" => SqlCoreType.SmallMoney,
            "FLOAT" => SqlCoreType.Float,
            "REAL" => SqlCoreType.Real,
            "DATE" => SqlCoreType.Date,
            "TIME" => SqlCoreType.Time,
            "DATETIME" => SqlCoreType.DateTime,
            "DATETIME2" => SqlCoreType.DateTime2,
            "DATETIMEOFFSET" => SqlCoreType.DateTimeOffset,
            "SMALLDATETIME" => SqlCoreType.SmallDateTime,
            "CHAR" => SqlCoreType.Char,
            "NCHAR" => SqlCoreType.NChar,
            "VARCHAR" => SqlCoreType.VarChar,
            "NVARCHAR" => SqlCoreType.NVarChar,
            "TEXT" => SqlCoreType.Text,
            "NTEXT" => SqlCoreType.NText,
            "BINARY" => SqlCoreType.Binary,
            "VARBINARY" => SqlCoreType.VarBinary,
            "IMAGE" => SqlCoreType.Image,
            "UNIQUEIDENTIFIER" => SqlCoreType.UniqueIdentifier,
            "XML" => SqlCoreType.Xml,
            "HIERARCHYID" => SqlCoreType.HierarchyId,
            "GEOGRAPHY" => SqlCoreType.Geography,
            "GEOMETRY" => SqlCoreType.Geometry,
            _ => SqlCoreType.Unknown
        };

        // Parse parameters based on core type
        SqlTypeParameters? typeParams = null;
        if (parameters.Length > 0)
        {
            switch (coreType)
            {
                case SqlCoreType.Decimal:
                case SqlCoreType.Numeric:
                    if (parameters.Length >= 2 &&
                        int.TryParse(parameters[0], out int precision) &&
                        int.TryParse(parameters[1], out int scale))
                    {
                        typeParams = SqlTypeParameters.ForDecimal(precision, scale);
                    }
                    break;

                case SqlCoreType.Char:
                case SqlCoreType.NChar:
                case SqlCoreType.VarChar:
                case SqlCoreType.NVarChar:
                case SqlCoreType.Binary:
                case SqlCoreType.VarBinary:
                    if (parameters.Length == 1)
                    {
                        if (parameters[0].Equals("MAX", StringComparison.OrdinalIgnoreCase))
                        {
                            typeParams = SqlTypeParameters.ForMaxLengthString();
                        }
                        else if (int.TryParse(parameters[0], out int maxLength))
                        {
                            typeParams = SqlTypeParameters.ForFixedLengthString(maxLength);
                        }
                    }
                    break;
            }
        }

        return (coreType, typeParams);
    }

    private SqlTypeParameters? ParseTypeParameters(SqlCoreType coreType, List<string> parameters)
    {
        switch (coreType)
        {
            case SqlCoreType.Decimal:
            case SqlCoreType.Numeric:
                if (parameters.Count >= 2 &&
                    int.TryParse(parameters[0], out int precision) &&
                    int.TryParse(parameters[1], out int scale))
                {
                    return SqlTypeParameters.ForDecimal(precision, scale);
                }
                break;

            case SqlCoreType.Char:
            case SqlCoreType.NChar:
            case SqlCoreType.VarChar:
            case SqlCoreType.NVarChar:
            case SqlCoreType.Binary:
            case SqlCoreType.VarBinary:
                if (parameters.Count == 1)
                {
                    if (parameters[0].Equals("MAX", StringComparison.OrdinalIgnoreCase))
                    {
                        return SqlTypeParameters.ForMaxLengthString();
                    }
                    if (int.TryParse(parameters[0], out int maxLength))
                    {
                        return SqlTypeParameters.ForFixedLengthString(maxLength);
                    }
                }
                break;
        }
        return null;
    }
}

