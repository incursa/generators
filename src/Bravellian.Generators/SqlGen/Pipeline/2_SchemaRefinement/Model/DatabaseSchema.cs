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
/// Represents the resolved database schema with types and relationships.
/// This is the output of the second phase (type resolution).
/// </summary>
public class DatabaseSchema
{
    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string? DatabaseName { get; }

    /// <summary>
    /// Gets the collection of database objects (tables and views).
    /// </summary>
    public List<DatabaseObject> Objects { get; set; } = [];

    /// <summary>
    /// Gets or sets a dictionary of database objects by their fully qualified name.
    /// </summary>
    public Dictionary<string, DatabaseObject> ObjectsByName { get; set; } = [];

    /// <summary>
    /// Creates a new database schema.
    /// </summary>
    /// <param name="databaseName">The name of the database.</param>
    public DatabaseSchema(string? databaseName = null)
    {
        this.DatabaseName = databaseName;
    }

    /// <summary>
    /// Adds a database object to the schema.
    /// </summary>
    /// <param name="databaseObject">The database object to add.</param>
    public void AddObject(DatabaseObject databaseObject)
    {
        this.Objects.Add(databaseObject);
        this.ObjectsByName[databaseObject.FullName] = databaseObject;
    }
}

