// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators.SqlGen.Pipeline;

using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

/// <summary>
/// Represents a SQL table or view definition.
/// </summary>
public class PwTableDefinition(
    string schema,
    string name,
    List<PwColumnDefinition> columns,
    bool isView = false,
    CreateViewStatement? viewDefinition = null,
    CreateTableStatement? underlyingTableDefinition = null)
{
    public string Schema { get; } = schema;

    public string Name { get; } = name;

    public List<PwColumnDefinition> Columns { get; } = columns;

    public string EntityName => this.Name; // $"{this.Schema}_{this.Name}";

    /// <summary>
    /// Gets whether this definition represents a view.
    /// </summary>
    public bool IsView { get; } = isView;

    /// <summary>
    /// Gets the view definition if this is a view.
    /// </summary>
    public CreateViewStatement? ViewDefinition { get; } = viewDefinition;

    /// <summary>
    /// Gets the underlying table definition that contains constraints and other metadata.
    /// </summary>
    public CreateTableStatement? UnderlyingTableDefinition { get; } = underlyingTableDefinition;
}

