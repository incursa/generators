// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;

using System.Collections.Generic;

/// <summary>
/// Represents a database index definition.
/// </summary>
public class IndexDefinition
{
    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this index is unique.
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// Gets a value indicating whether this index is clustered.
    /// </summary>
    public bool IsClustered { get; }

    /// <summary>
    /// Gets the column names included in this index.
    /// </summary>
    public List<string> ColumnNames { get; } = [];

    /// <summary>
    /// Creates a new index definition.
    /// </summary>
    /// <param name="name">The name of the index.</param>
    /// <param name="isUnique">Whether this index is unique.</param>
    /// <param name="isClustered">Whether this index is clustered.</param>
    public IndexDefinition(string name, bool isUnique, bool isClustered)
    {
        Name = name;
        IsUnique = isUnique;
        IsClustered = isClustered;
    }

    /// <summary>
    /// Creates a new index definition.
    /// </summary>
    /// <param name="name">The name of the index.</param>
    /// <param name="isUnique">Whether this index is unique.</param>
    /// <param name="isClustered">Whether this index is clustered.</param>
    /// <param name="columnNames">The column names included in this index.</param>
    public IndexDefinition(string name, bool isUnique, bool isClustered, IEnumerable<string> columnNames)
        : this(name, isUnique, isClustered)
    {
        this.ColumnNames.AddRange(columnNames);
    }
}

