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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bravellian.Generators.SqlGen.Common.Configuration
{
    /// <summary>
    /// Tracks which parts of the SqlConfiguration are used during the generation process.
    /// This class is thread-safe.
    /// </summary>
    public class UsedConfigurationTracker
    {
        private readonly ConcurrentDictionary<string, JsonNode?> _usedGlobalMappings = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonNode?>> _usedTableConfigs = new();
        private readonly JsonNode? _originalConfigNode;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsedConfigurationTracker"/> class.
        /// </summary>
        /// <param name="originalJsonConfig">The original raw JSON string of the configuration.</param>
        public UsedConfigurationTracker(string? originalJsonConfig)
        {
            if (!string.IsNullOrWhiteSpace(originalJsonConfig))
            {
                try
                {
                    _originalConfigNode = JsonNode.Parse(originalJsonConfig, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException)
                {
                    _originalConfigNode = null;
                }
            }
        }

        /// <summary>
        /// Marks a global type mapping rule as used.
        /// </summary>
        /// <param name="rule">The global type mapping rule that was used.</param>
        public void MarkGlobalMappingUsed(GlobalTypeMapping rule)
        {
            if (_originalConfigNode?["globalTypeMappings"] is not JsonArray globalMappings) return;

            var ruleNode = globalMappings.FirstOrDefault(node =>
                node?["apply"]?["csharpType"]?.GetValue<string>() == rule.Apply.CSharpType &&
                node?["match"]?["sqlType"]?.ToString() == rule.Match.SqlType?.ToString() &&
                (node?["match"]?["columnNameRegex"]?.GetValue<string>() ?? "") == (rule.Match.ColumnNameRegex ?? "") &&
                (node?["match"]?["tableNameRegex"]?.GetValue<string>() ?? "") == (rule.Match.TableNameRegex ?? "") &&
                (node?["match"]?["schemaNameRegex"]?.GetValue<string>() ?? "") == (rule.Match.SchemaNameRegex ?? "")
            );

            if (ruleNode != null)
            {
                _usedGlobalMappings.TryAdd(ruleNode.ToJsonString(), ruleNode.DeepClone());
            }
        }

        /// <summary>
        /// Marks a table-level configuration property as used.
        /// </summary>
        /// <param name="tableKey">The schema-qualified table name (e.g., "dbo.Users").</param>
        /// <param name="property">The name of the property that was used (e.g., "csharpClassName").</param>
        public void MarkTablePropertyUsed(string tableKey, string property)
        {
            if (_originalConfigNode?["tables"]?[tableKey]?[property] is not JsonNode propertyNode) return;

            var tableDict = _usedTableConfigs.GetOrAdd(tableKey, _ => new ConcurrentDictionary<string, JsonNode?>());
            tableDict.TryAdd(property, propertyNode.DeepClone());
        }

        /// <summary>
        /// Marks a column override property as used.
        /// </summary>
        /// <param name="tableKey">The schema-qualified table name.</param>
        /// <param name="columnName">The name of the column.</param>
        /// <param name="property">The name of the property used (e.g., "sqlType").</param>
        public void MarkColumnOverrideUsed(string tableKey, string columnName, string property)
        {
            if (_originalConfigNode?["tables"]?[tableKey]?["columnOverrides"]?[columnName]?[property] is not JsonNode propertyNode) return;

            var tableDict = _usedTableConfigs.GetOrAdd(tableKey, _ => new ConcurrentDictionary<string, JsonNode?>());
            var columnOverrides = tableDict.GetOrAdd("columnOverrides", _ => new JsonObject()) as JsonObject;

            if (columnOverrides != null)
            {
                if (!columnOverrides.ContainsKey(columnName))
                {
                    columnOverrides[columnName] = new JsonObject();
                }
                (columnOverrides[columnName] as JsonObject)![property] = propertyNode.DeepClone();
            }
        }

        /// <summary>
        /// Generates a JSON string representing only the used portions of the configuration.
        /// </summary>
        /// <returns>A JSON string of the used configuration, or null if no configuration was used.</returns>
        public string? GetUsedConfigurationAsJson()
        {
            if (_usedGlobalMappings.IsEmpty && _usedTableConfigs.IsEmpty)
            {
                return null;
            }

            var root = new JsonObject();

            if (!_usedGlobalMappings.IsEmpty)
            {
                root["globalTypeMappings"] = new JsonArray(_usedGlobalMappings.Values.ToArray());
            }

            if (!_usedTableConfigs.IsEmpty)
            {
                var tablesNode = new JsonObject();
                foreach (var tableEntry in _usedTableConfigs.OrderBy(kv => kv.Key))
                {
                    var tableNode = new JsonObject();
                    foreach (var propEntry in tableEntry.Value.OrderBy(kv => kv.Key))
                    {
                        tableNode[propEntry.Key] = propEntry.Value;
                    }
                    tablesNode[tableEntry.Key] = tableNode;
                }
                root["tables"] = tablesNode;
            }

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }
}