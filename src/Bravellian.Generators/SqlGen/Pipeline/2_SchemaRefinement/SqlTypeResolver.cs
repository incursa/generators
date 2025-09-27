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

using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Common.Configuration;

namespace Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement
{
    /// <summary>
    /// Implements Phase 2 of the pipeline. This phase is responsible for "patching" the raw schema model
    /// with information from the configuration file. Its primary purpose is to fix missing type or
    /// nullability information for columns in SQL views, where this cannot be inferred from the DDL.
    /// </summary>
    public class SqlTypeResolver : ITypeResolver
    {
        private readonly IBvLogger _logger;

        public SqlTypeResolver(IBvLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Refines the raw database model by applying SQL type and nullability overrides from the configuration.
        /// This phase ONLY patches the raw schema - it does not perform C# type resolution.
        /// </summary>
        /// <param name="databaseModel">The raw database model from Phase 1.</param>
        /// <param name="configuration">Configuration containing columnOverrides for schema refinement.</param>
        /// <returns>The refined database model with patched SQL types and nullability.</returns>
        public RawDatabaseSchema Resolve(RawDatabaseSchema databaseModel, SqlConfiguration configuration = null)
        {
            _logger.LogMessage("Phase 2: Refining schema model with configuration overrides...");
            
            if (configuration == null)
            {
                _logger.LogMessage("No configuration provided - skipping schema refinement.");
                return databaseModel;
            }

            // Process tables
            foreach (var table in databaseModel.Tables)
            {
                RefineTableSchema(table, configuration);
            }

            // Process views
            foreach (var view in databaseModel.Views)
            {
                RefineViewSchema(view, configuration);
            }

            _logger.LogMessage("Phase 2: Schema refinement completed.");
            return databaseModel;
        }

        private void RefineTableSchema(Table table, SqlConfiguration configuration)
        {
            _logger.LogMessage($"Refining schema for table {table.Schema}.{table.Name}");
            
            foreach (var column in table.Columns)
            {
                ApplySchemaRefinements(column, table.Schema, table.Name, configuration);
            }
        }

        private void RefineViewSchema(View view, SqlConfiguration configuration)
        {
            _logger.LogMessage($"Refining schema for view {view.Schema}.{view.Name}");
            
            foreach (var column in view.Columns)
            {
                ApplySchemaRefinements(column, view.Schema, view.Name, configuration);
            }
        }

        /// <summary>
        /// Applies only Phase 2 refinements: sqlType and isNullable overrides from columnOverrides.
        /// This method specifically avoids any C# type resolution logic.
        /// </summary>
        private void ApplySchemaRefinements(Column column, string schema, string objectName, SqlConfiguration configuration)
        {
            var columnOverride = configuration.GetColumnOverride(schema, objectName, column.Name);
            
            if (columnOverride == null)
            {
                return;
            }

            // Phase 2 Responsibility: Patch the raw SQL schema.
            // If the config provides a sqlType, use it. This is critical for views.
            if (!string.IsNullOrWhiteSpace(columnOverride.SqlType))
            {
                var originalSqlType = column.SqlType;
                column.SqlType = columnOverride.SqlType;
                _logger.LogMessage($"Applied SQL type override for {schema}.{objectName}.{column.Name}: {originalSqlType} -> {column.SqlType}");
            }

            // If the config provides a nullability override, use it. This is also for views.
            if (columnOverride.IsNullable.HasValue)
            {
                var originalNullability = column.IsNullable;
                column.IsNullable = columnOverride.IsNullable.Value;
                _logger.LogMessage($"Applied nullability override for {schema}.{objectName}.{column.Name}: {originalNullability} -> {column.IsNullable}");
            }

            // NOTE: We explicitly do NOT touch columnOverride.CSharpType here.
            // That is the responsibility of Phase 3 (C# Model Transformation).
        }
    }
}
