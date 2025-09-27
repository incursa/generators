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

using System.Collections.Generic;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator._2_SchemaRefinement;

public class SqlTypeResolverTests
{
    private readonly TestLogger logger = new ();

    private RawDatabaseSchema CreateBasicRawSchema()
    {
        var createTable = new CreateTableStatement
        {
            SchemaObjectName = new SchemaObjectName
            {
                Identifiers = { new Identifier { Value = "dbo" }, new Identifier { Value = "MyTable" } },
            },
            Definition = new TableDefinition
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition
                    {
                        ColumnIdentifier = new Identifier { Value = "Id" },
                        DataType = new SqlDataTypeReference { SqlDataTypeOption = SqlDataTypeOption.Int }
                    },
                },
            },
        };
        return new RawDatabaseSchema { TableStatements = { createTable } };
    }

    private static DatabaseObject? FindObjectByName(DatabaseSchema schema, string schemaName, string objectName)
    {
        return schema.Objects.Find(t => string.Equals(t.Schema, schemaName, StringComparison.Ordinal) && string.Equals(t.Name, objectName, StringComparison.Ordinal));
    }

    private static DatabaseColumn? FindColumnByName(DatabaseObject obj, string columnName)
    {
        return obj.Columns.Find(c => string.Equals(c.Name, columnName, StringComparison.Ordinal));
    }
}
