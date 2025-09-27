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

using System.Collections.Generic;

/// <summary>
/// Represents a database object (table or view).
/// </summary>
public class DatabaseObject
{
    /// <summary>
    /// Gets the schema name.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the fully qualified name of the database object (schema.name).
    /// </summary>
    public string FullName => $"{this.Schema}.{this.Name}";

    /// <summary>
    /// Gets a value indicating whether this object is a view.
    /// </summary>
    public bool IsView { get; }

    /// <summary>
    /// Gets the collection of columns in this database object.
    /// </summary>
    public List<DatabaseColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets the collection of primary key column names.
    /// </summary>
    public HashSet<string> PrimaryKeyColumns { get; set; } = [];
    
    /// <summary>
    /// Gets the collection of indexes defined on this database object.
    /// </summary>
    public List<IndexDefinition> Indexes { get; set; } = [];

    /// <summary>
    /// Creates a new database object.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="name">The object name.</param>
    /// <param name="isView">Whether this object is a view.</param>
    public DatabaseObject(string schema, string name, bool isView)
    {
        this.Schema = schema;
        this.Name = name;
        this.IsView = isView;
    }

    /// <summary>
    /// Adds a column to this database object.
    /// </summary>
    /// <param name="column">The column to add.</param>
    public void AddColumn(DatabaseColumn column)
    {
        this.Columns.Add(column);
        if (column.IsPrimaryKey)
        {
            this.PrimaryKeyColumns.Add(column.Name);
        }
    }
}

