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

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bravellian.Generators.SqlGen.Common.Configuration
{
    /// <summary>
    /// Configuration for SQL code generation
    /// </summary>
    public class SqlConfiguration
    {
        /// <summary>
        /// The namespace to use for generated entities.
        /// </summary>
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = "Generated.Entities";

        /// <summary>
        /// Whether to generate navigation properties between related entities.
        /// </summary>
        [JsonPropertyName("generateNavigationProperties")]
        public bool GenerateNavigationProperties { get; set; } = true;

        /// <summary>
        /// Whether to generate a DbContext class.
        /// </summary>
        [JsonPropertyName("generateDbContext")]
        public bool GenerateDbContext { get; set; } = true;

        /// <summary>
        /// The base class for the generated DbContext.
        /// </summary>
        [JsonPropertyName("dbContextName")]
        public string DbContextName { get; set; } = "AppDbContext";

        /// <summary>
        /// The base class for the generated DbContext.
        /// </summary>
        [JsonPropertyName("dbContextBaseClass")]
        public string DbContextBaseClass { get; set; } = "BravellianDbContextBase";

        /// <summary>
        /// Global type mapping rules
        /// </summary>
        [JsonPropertyName("ignoreSchemas")]
        public List<string> IgnoreSchemas { get; set; } = new List<string>();

        /// <summary>
        /// Global type mapping rules
        /// </summary>
        [JsonPropertyName("globalTypeMappings")]
        public List<GlobalTypeMapping> GlobalTypeMappings { get; set; } = new List<GlobalTypeMapping>();

        /// <summary>
        /// Table-specific configuration
        /// </summary>
        [JsonPropertyName("tables")]
        public Dictionary<string, TableConfiguration> Tables { get; set; } = new Dictionary<string, TableConfiguration>();

        /// <summary>
        /// Gets a column override for a specific table and column
        /// </summary>
        public ColumnOverride GetColumnOverride(string schema, string tableName, string columnName)
        {
            string key = $"{schema}.{tableName}";
            if (Tables.TryGetValue(key, out var tableConfig) && 
                tableConfig.ColumnOverrides != null &&
                tableConfig.ColumnOverrides.TryGetValue(columnName, out var columnOverride))
            {
                return columnOverride;
            }
            
            return null;
        }

        /// <summary>
        /// Creates a configuration from a JSON string.
        /// </summary>
        /// <param name="json">The JSON content to parse.</param>
        /// <returns>A new SqlConfiguration instance, or null if parsing fails.</returns>
        public static SqlConfiguration FromJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                
                return JsonSerializer.Deserialize<SqlConfiguration>(json, options);
            }
            catch (JsonException)
            {
                // Return null if invalid JSON is provided
                return null;
            }
        }
    }

    /// <summary>
    /// Configuration for a specific table
    /// </summary>
    public class TableConfiguration
    {
        /// <summary>
        /// Optional description for the table configuration.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The C# class name to use for this table
        /// </summary>
        [JsonPropertyName("csharpClassName")]
        public string CSharpClassName { get; set; }

        /// <summary>
        /// Override for the primary key columns
        /// </summary>
        [JsonPropertyName("primaryKeyOverride")]
        public HashSet<string> PrimaryKeyOverride { get; set; }
        
        /// <summary>
        /// The column name to use for multi-tenant/scoping functionality.
        /// This column will be included in all data access method WHERE clauses.
        /// </summary>
        [JsonPropertyName("scopeKey")]
        public string? ScopeKey { get; set; }

        /// <summary>
        /// Column-specific overrides, keyed by column name
        /// </summary>
        [JsonPropertyName("columnOverrides")]
        public Dictionary<string, ColumnOverride> ColumnOverrides { get; set; } = new Dictionary<string, ColumnOverride>();

        /// <summary>
        /// Configuration for update operations
        /// </summary>
        [JsonPropertyName("updateConfig")]
        public UpdateConfig UpdateConfig { get; set; }

        /// <summary>
        /// Custom read methods
        /// </summary>
        [JsonPropertyName("readMethods")]
        public List<ReadMethod> ReadMethods { get; set; }
    }

    /// <summary>
    /// Global type mapping rule
    /// </summary>
    public class GlobalTypeMapping
    {
        /// <summary>
        /// Optional description for the rule.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Rule priority - higher values take precedence
        /// </summary>
        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        /// <summary>
        /// Match conditions
        /// </summary>
        [JsonPropertyName("match")]
        public GlobalTypeMappingMatch Match { get; set; }

        /// <summary>
        /// Apply actions
        /// </summary>
        [JsonPropertyName("apply")]
        public GlobalTypeMappingApply Apply { get; set; }
    }

    /// <summary>
    /// Match conditions for global type mapping
    /// </summary>
    public class GlobalTypeMappingMatch
    {
        /// <summary>
        /// Regex pattern to match column names
        /// </summary>
        [JsonPropertyName("columnNameRegex")]
        public string ColumnNameRegex { get; set; }

        /// <summary>
        /// Regex pattern to match table names
        /// </summary>
        [JsonPropertyName("tableNameRegex")]
        public string TableNameRegex { get; set; }

        /// <summary>
        /// Regex pattern to match schema names
        /// </summary>
        [JsonPropertyName("schemaNameRegex")]
        public string SchemaNameRegex { get; set; }

        /// <summary>
        /// SQL type to match (can be string or array of strings)
        /// </summary>
        [JsonPropertyName("sqlType")]
        [JsonConverter(typeof(StringOrStringArrayConverter))]
        public List<string> SqlType { get; set; } = new List<string>();
    }

    /// <summary>
    /// Apply actions for global type mapping
    /// </summary>
    public class GlobalTypeMappingApply
    {
        /// <summary>
        /// C# type to apply
        /// </summary>
        [JsonPropertyName("csharpType")]
        public string CSharpType { get; set; }
    }

    /// <summary>
    /// Column-specific override
    /// </summary>
    public class ColumnOverride
    {
        /// <summary>
        /// Optional description for the column override.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        /// <summary>
        /// SQL type override (for views). Used in Phase 2.
        /// </summary>
        [JsonPropertyName("sqlType")]
        public string SqlType { get; set; }

        /// <summary>
        /// Nullability override (for views). Used in Phase 2.
        /// </summary>
        [JsonPropertyName("isNullable")]
        public bool? IsNullable { get; set; }

        /// <summary>
        /// C# type override. Used in Phase 3. This is the ultimate override.
        /// </summary>
        [JsonPropertyName("csharpType")]
        public string CSharpType { get; set; }

        /// <summary>
        /// C# property name override. Used in Phase 3.
        /// </summary>
        [JsonPropertyName("csharpPropertyName")]
        public string CSharpPropertyName { get; set; }
    }

    /// <summary>
    /// Update operation configuration
    /// </summary>
    public class UpdateConfig
    {
        /// <summary>
        /// Columns to exclude from update operations
        /// </summary>
        [JsonPropertyName("ignoreColumns")]
        public List<string> IgnoreColumns { get; set; }
    }

    /// <summary>
    /// Custom read method definition
    /// </summary>
    public class ReadMethod
    {
        /// <summary>
        /// Method name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Columns to match on
        /// </summary>
        [JsonPropertyName("matchColumns")]
        public List<string> MatchColumns { get; set; }
    }

    /// <summary>
    /// Custom JSON converter to handle a property that can be a single string or an array of strings.
    /// </summary>
    internal class StringOrStringArrayConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new List<string> { reader.GetString() };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<List<string>>(ref reader, options);
            }

            throw new JsonException("Expected a string or an array of strings.");
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            if (value.Count == 1)
            {
                writer.WriteStringValue(value[0]);
            }
            else
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}
