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

using Microsoft.SqlServer.TransactSql.ScriptDom;
using Bravellian.Generators.SqlGen.Common;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using System.Collections.Generic;
using System.Linq;

namespace Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement
{
    /// <summary>
    /// Helper class to process a QueryDerivedTable (a subquery in a FROM clause)
    /// and create a virtual DatabaseObject representing its output.
    /// </summary>
    internal class SubqueryProcessor
    {
        private readonly IBvLogger _logger;
        private readonly DatabaseSchema _databaseSchema;
        private readonly SchemaRefiner _schemaRefiner;

        public SubqueryProcessor(IBvLogger logger, DatabaseSchema databaseSchema, SchemaRefiner schemaRefiner)
        {
            _logger = logger;
            _databaseSchema = databaseSchema;
            _schemaRefiner = schemaRefiner;
        }

        /// <summary>
        /// Processes a derived table to create a virtual table object.
        /// </summary>
        /// <param name="derivedTable">The QueryDerivedTable from the AST.</param>
        /// <returns>A virtual DatabaseObject or null if processing fails.</returns>
        public DatabaseObject? Process(QueryDerivedTable derivedTable)
        {
            var alias = derivedTable.Alias?.Value;
            if (string.IsNullOrEmpty(alias))
            {
                _logger.LogMessage("WARNING: Found a subquery with no alias. Cannot process it.");
                return null;
            }

            if (derivedTable.QueryExpression is not QuerySpecification querySpec)
            {
                _logger.LogMessage($"WARNING: Subquery '{alias}' is not a simple SELECT statement. Columns will be indeterminate.");
                return new DatabaseObject("virtual", alias, true);
            }

            var virtualTable = new DatabaseObject("virtual", alias, true);
            var subqueryAliases = _schemaRefiner.ExtractTableAliases(querySpec.FromClause, _databaseSchema);

            foreach (var selectElement in querySpec.SelectElements.OfType<SelectScalarExpression>())
            {
                var (columnName, baseColumn) = _schemaRefiner.ResolveViewColumn(selectElement, subqueryAliases, _databaseSchema);

                if (string.IsNullOrEmpty(columnName)) continue;

                var dbColumn = new DatabaseColumn(
                    name: columnName,
                    databaseType: baseColumn?.DatabaseType ?? PwSqlType.Unknown,
                    isNullable: baseColumn?.IsNullable ?? true,
                    isPrimaryKey: false,
                    schema: "virtual",
                    tableName: alias
                )
                {
                    IsIndeterminate = (baseColumn == null)
                };

                virtualTable.Columns.Add(dbColumn);
            }

            return virtualTable;
        }
    }
}