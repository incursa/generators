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

namespace Bravellian.Generators.SqlGen.Pipeline.1_Ingestion.Model
{
    using System.Collections.Generic;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

/// <summary>
/// Represents the raw database schema model after ingestion.
/// </summary>
    public class RawDatabaseSchema
{
    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public required string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the table CREATE statements from the SQL files.
    /// </summary>
    public List<CreateTableStatement> TableStatements { get; set; } = new List<CreateTableStatement>();

    /// <summary>
    /// Gets or sets the view CREATE statements from the SQL files.
    /// </summary>
    public List<CreateViewStatement> ViewStatements { get; set; } = new List<CreateViewStatement>();

    /// <summary>
    /// Gets or sets the index CREATE statements from the SQL files.
    /// </summary>
    public List<CreateIndexStatement> IndexStatements { get; set; } = new List<CreateIndexStatement>();

    /// <summary>
    /// Gets or sets the tables in the database.
    /// </summary>
    public List<Table> Tables { get; set; } = new List<Table>();

    /// <summary>
    /// Gets or sets the views in the database.
    /// </summary>
    public List<View> Views { get; set; } = new List<View>();
}

/// <summary>
/// Represents a database table.
/// </summary>
    public class Table
{
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the columns in the table.
    /// </summary>
    public List<Column> Columns { get; set; } = new List<Column>();

    /// <summary>
    /// Gets or sets the primary key columns.
    /// </summary>
    public List<string> PrimaryKeyColumns { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the indexes on the table.
    /// </summary>
    public List<Index> Indexes { get; set; } = new List<Index>();
}

/// <summary>
/// Represents a database view.
/// </summary>
    public class View
{
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>
    /// Gets or sets the view name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the columns in the view.
    /// </summary>
    public List<Column> Columns { get; set; } = new List<Column>();
}

/// <summary>
/// Represents a database column.
/// </summary>
    public class Column
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the SQL data type.
    /// </summary>
    public required string SqlType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets the C# type that this SQL type maps to.
    /// </summary>
    public required string CSharpType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is part of a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the precision for numeric data types.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale for numeric data types.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for string data types.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the indexes this column participates in.
    /// </summary>
    public List<string> ParticipatingIndexes { get; set; } = new List<string>();
}

/// <summary>
/// Represents a database index.
/// </summary>
    public class Index
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the columns in the index.
    /// </summary>
    public List<string> Columns { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; }
}
}
