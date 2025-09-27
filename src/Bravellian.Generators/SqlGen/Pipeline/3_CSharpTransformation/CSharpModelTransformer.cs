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
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using Humanizer;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;

namespace Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation
{
    /// <summary>
    /// Phase 3: Transforms the refined database model into a C# model ready for code generation.
    /// This is where all C# type resolution logic lives, following the strict precedence rules.
    /// </summary>
    public class CSharpModelTransformer : ICSharpModelTransformer
    {
        private readonly IBvLogger _logger;
        private readonly SqlConfiguration _configuration;
        private readonly UsedConfigurationTracker? _usageTracker;

        public SqlConfiguration Configuration => _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="CSharpModelTransformer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The SQL configuration.</param>
        public CSharpModelTransformer(IBvLogger logger, SqlConfiguration configuration, UsedConfigurationTracker? usageTracker)
        {
            _logger = logger;
            _configuration = configuration ?? new SqlConfiguration();
            this._usageTracker = usageTracker;
        }

        /// <inheritdoc/>
        public GenerationModel Transform(DatabaseSchema databaseSchema)
        {
            _logger.LogMessage("Phase 3: Transforming refined database model to C# model...");

            var model = new GenerationModel();

            var ignoredSchemas = new HashSet<string>(_configuration.IgnoreSchemas ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var objectsToProcess = databaseSchema.Objects;

            if (ignoredSchemas.Any())
            {
                _logger.LogMessage($"Ignoring schemas: {string.Join(", ", ignoredSchemas)}");
                objectsToProcess = objectsToProcess.Where(o => !ignoredSchemas.Contains(o.Schema)).ToList();
            }

            // Transform database objects to classes
            foreach (var dbObject in objectsToProcess)
            {
                var classModel = TransformDatabaseObject(dbObject);
                model.Classes.Add(classModel);
            }

            _logger.LogMessage("Phase 3: C# model transformation completed.");
            return model;
        }

        private ClassModel TransformDatabaseObject(DatabaseObject dbObject)
        {
            string qualifiedName = $"{dbObject.Schema}.{dbObject.Name}";
            var tableConfig = _configuration.Tables.ContainsKey(qualifiedName) ? _configuration.Tables[qualifiedName] : null;
            var className = tableConfig?.CSharpClassName ?? dbObject.Name.Pascalize();

            // Determine the definitive primary key columns, respecting the override.
            var primaryKeyColumns = (tableConfig?.PrimaryKeyOverride != null && tableConfig.PrimaryKeyOverride.Any())
                ? new HashSet<string>(tableConfig.PrimaryKeyOverride, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(dbObject.PrimaryKeyColumns, StringComparer.OrdinalIgnoreCase);

            // Construct the final namespace using the base from the configuration and the schema name.
            var schemaNamespacePart = dbObject.Schema.Pascalize();
            var finalNamespace = !string.IsNullOrEmpty(_configuration.Namespace)
                ? $"{_configuration.Namespace}.{schemaNamespacePart}"
                : schemaNamespacePart;

            var classModel = new ClassModel
            {
                Name = className,
                Namespace = finalNamespace, // Apply the constructed namespace
                SourceObjectName = dbObject.Name,
                SourceSchemaName = dbObject.Schema,
                IsView = dbObject.IsView,
                Indexes = dbObject.Indexes.Select(i => new IndexModel
                {
                    Name = i.Name,
                    IsUnique = i.IsUnique,
                    Columns = i.ColumnNames,
                }).ToList()
            };

            // Transform columns to properties
            foreach (var column in dbObject.Columns)
            {
                // Get column-specific configuration
                ColumnOverride columnOverride = null;
                tableConfig?.ColumnOverrides?.TryGetValue(column.Name, out columnOverride);

                // Determine property name - use override if specified
                var propertyName = columnOverride?.CSharpPropertyName ?? column.Name;

                // Resolve C# type with proper precedence rules
                var (csharpType, isOverridden) = ResolveCSharpType(column, dbObject.Schema, dbObject.Name, out var typeSourceInfo);

                var property = new PropertyModel
                {
                    Name = propertyName,
                    Type = csharpType,
                    IsPrimaryKey = primaryKeyColumns.Contains(column.Name),
                    SourceColumnName = column.Name,
                    SourceSqlType = column.DatabaseType,
                    IsNullable = column.IsNullable,
                    IsComputed = false, //column.IsComputed,
                    IsIndeterminate = column.IsIndeterminate,
                    IsTypeOverridden = isOverridden,
                };

                // Populate the audit trail
                if (column.SourceInfo is PropertySourceInfo colSource)
                {
                    property.SourceAuditTrail.Add(colSource);
                }
                property.SourceAuditTrail.Add(typeSourceInfo);

                if (columnOverride?.CSharpPropertyName != null)
                {
                    property.SourceAuditTrail.Add(new PropertySourceInfo("Property Name", "Column Override", $"Set to '{propertyName}' by configuration."));
                    property.IsTypeOverridden = true;
                }
                else
                {
                    property.SourceAuditTrail.Add(new PropertySourceInfo("Property Name", "Default Convention", $"Derived directly from source column '{column.Name}'."));
                }


                classModel.Properties.Add(property);
            }

            // Store the primary key properties for easier access in method generation
            classModel.PrimaryKeyProperties = classModel.Properties
                .Where(p => p.IsPrimaryKey)
                .ToList();

            // Identify and store the scope key property
            if (!string.IsNullOrWhiteSpace(tableConfig?.ScopeKey))
            {
                classModel.ScopeKeyProperty = classModel.Properties.FirstOrDefault(p =>
                    string.Equals(p.SourceColumnName, tableConfig.ScopeKey, StringComparison.OrdinalIgnoreCase));

                if (classModel.ScopeKeyProperty == null)
                {
                    _logger.LogWarning($"Scope key '{tableConfig.ScopeKey}' was configured for {dbObject.FullName}, but no matching column was found.");
                }
                else
                {
                    _logger.LogMessage($"Using '{classModel.ScopeKeyProperty.Name}' as scope key for {dbObject.FullName}");
                }
            }

            // Generate data access methods (only for tables, not views)
            if (!dbObject.IsView)
            {
                // Generate input models first, as data access methods depend on them.
                classModel.CreateInput = GenerateCreateInputModel(classModel, dbObject, tableConfig);
                classModel.UpdateInput = GenerateUpdateInputModel(classModel, dbObject, tableConfig);

                // Now that input models exist, generate the methods that use them.
                GenerateDataAccessMethods(classModel, dbObject, tableConfig);
            }
            else
            {
                GenerateViewAccessMethods(classModel, dbObject, tableConfig);
            }

            return classModel;
        }

        /// <summary>
        /// Generates data access methods for a database object.
        /// The method generation is driven by configuration, with the physical schema as a fallback.
        /// </summary>
        /// <param name="classModel">The class model to generate methods for</param>
        /// <param name="dbObject">The database object (table)</param>
        /// <param name="tableConfig">Optional configuration overrides for the table</param>


        private void GenerateDataAccessMethods(ClassModel classModel, DatabaseObject dbObject, TableConfiguration? tableConfig)
        {
            // 1. Primary Key Methods (Read, Update, Delete)
            // We now use the primary key properties directly
            var primaryKeyProperties = classModel.PrimaryKeyProperties;

            // Use the class model's scope key property
            var scopeKeyProperty = classModel.ScopeKeyProperty;

            // Helper function to add scope key if not already a parameter
            Action<MethodModel> addScopeKeyIfNeeded = (method) =>
            {
                if (scopeKeyProperty == null)
                    return;

                // Check if any existing parameter uses the same source property
                bool alreadyHasScopeKey = method.Parameters.Any(p =>
                    string.Equals(p.SourcePropertyName, scopeKeyProperty.Name, StringComparison.OrdinalIgnoreCase));

                if (!alreadyHasScopeKey)
                {
                    // Add the scope key parameter at the beginning
                    method.Parameters.Insert(0, new ParameterModel
                    {
                        Name = scopeKeyProperty.Name.Camelize(),
                        Type = scopeKeyProperty.Type,
                        SourcePropertyName = scopeKeyProperty.Name,
                        IsScopeKey = true
                    });
                }
            };

            // Helper function to check for duplicate methods before adding
            Action<MethodModel> addMethodIfNotDuplicate = (newMethod) =>
            {
                bool isDuplicate = classModel.Methods.Any(existingMethod =>
                    existingMethod.Name == newMethod.Name &&
                    existingMethod.Parameters.Count == newMethod.Parameters.Count &&
                    existingMethod.Parameters.Select(p => p.Type)
                        .SequenceEqual(newMethod.Parameters.Select(p => p.Type)));

                if (!isDuplicate)
                {
                    classModel.Methods.Add(newMethod);
                }
                else
                {
                    _logger.LogWarning($"Skipping duplicate method generation for '{newMethod.Name}' on class '{classModel.Name}'.");
                }
            };

            // Generate Read method based on primary key
            if (primaryKeyProperties.Any())
            {
                var readMethod = new MethodModel
                {
                    Name = $"Get{classModel.Name}",
                    Type = MethodType.Read,
                    ReturnType = classModel.Name,
                    IsPrimaryKeyMethod = true
                };

                foreach (var pkProperty in primaryKeyProperties)
                {
                    readMethod.Parameters.Add(new ParameterModel
                    {
                        Name = pkProperty.Name.Camelize(),
                        Type = pkProperty.Type,
                        SourcePropertyName = pkProperty.Name
                    });
                }

                // Add scope key parameter using our helper method
                addScopeKeyIfNeeded(readMethod);

                addMethodIfNotDuplicate(readMethod);
            }

            // 2. Read Methods
            // First check if readMethods are specified in configuration
            if (tableConfig?.ReadMethods != null && tableConfig.ReadMethods.Any())
            {
                _logger.LogMessage($"Using configured read methods for {dbObject.FullName}");
                GenerateCustomReadMethods(classModel, tableConfig);
            }
            // If not, fall back to convention of creating a read method for each INDEX
            else
            {
                _logger.LogMessage($"Falling back to index-based read methods for {dbObject.FullName}");
                GenerateIndexBasedReadMethods(classModel, dbObject, tableConfig);
            }

            // Only generate methods that rely on a primary key if one is defined
            if (primaryKeyProperties.Any())
            {
                // ... method generation for Create, Update,
                var createMethod = new MethodModel
                {
                    Name = $"Create{classModel.Name}",
                    Type = MethodType.Create,
                    ReturnType = classModel.Name,
                    Parameters = new List<ParameterModel>
                {
                    new ParameterModel
                    {
                        Name = "input", // Use the input model instead of the entity
                        Type = classModel.CreateInput.Name
                    }
                }
                };

                // Add scope key parameter using our helper method
                addScopeKeyIfNeeded(createMethod);

                // Store scope key property in metadata for the code generator
                if (scopeKeyProperty != null)
                {
                    createMethod.Metadata["ScopeKey"] = scopeKeyProperty;
                }

                addMethodIfNotDuplicate(createMethod);

                var updateMethod = new MethodModel
                {
                    Name = $"Update{classModel.Name}",
                    Type = MethodType.Update,
                    ReturnType = classModel.Name, // Return the updated entity
                    Parameters = new List<ParameterModel>()
                };

                // Add primary key parameters first
                foreach (var pkProperty in primaryKeyProperties)
                {
                    updateMethod.Parameters.Add(new ParameterModel
                    {
                        Name = pkProperty.Name.Camelize(),
                        Type = pkProperty.Type,
                        SourcePropertyName = pkProperty.Name
                    });
                }

                // Add the update input parameter
                updateMethod.Parameters.Add(new ParameterModel
                {
                    Name = "input",
                    Type = classModel.UpdateInput.Name
                });

                // Add scope key parameter using our helper method
                addScopeKeyIfNeeded(updateMethod);

                // Add metadata about which columns to include/exclude in the update
                // We need to track source column names, not property names
                var ignoredSourceColumns = new HashSet<string>(
                    primaryKeyProperties.Select(p => p.SourceColumnName),
                    StringComparer.OrdinalIgnoreCase);

                if (tableConfig?.UpdateConfig?.IgnoreColumns != null)
                {
                    foreach (var columnName in tableConfig.UpdateConfig.IgnoreColumns)
                    {
                        ignoredSourceColumns.Add(columnName);
                    }

                    _logger.LogMessage($"Ignoring columns in UPDATE for {dbObject.FullName}: {string.Join(", ", tableConfig.UpdateConfig.IgnoreColumns)}");
                }

                updateMethod.Metadata["IgnoredSourceColumns"] = ignoredSourceColumns;
                addMethodIfNotDuplicate(updateMethod);

                // 5. Delete method - uses the same primary key as the Get method
                var deleteMethod = new MethodModel
                {
                    Name = $"Delete{classModel.Name}",
                    Type = MethodType.Delete,
                    ReturnType = "void",
                    IsPrimaryKeyMethod = true
                };

                foreach (var pkProperty in primaryKeyProperties)
                {
                    deleteMethod.Parameters.Add(new ParameterModel
                    {
                        Name = pkProperty.Name.Camelize(),
                        Type = pkProperty.Type,
                        SourcePropertyName = pkProperty.Name
                    });
                }

                // Add scope key parameter using our helper method
                addScopeKeyIfNeeded(deleteMethod);

                addMethodIfNotDuplicate(deleteMethod);
            }
            else
            {
                _logger.LogMessage($"No primary key defined for {dbObject.FullName}, skipping Create/Update/Delete methods");
            }
        }

        /// <summary>
        /// Generates data access methods for views.
        /// For views, we typically generate a GetAll method and any configured read methods.
        /// </summary>
        private void GenerateViewAccessMethods(ClassModel classModel, DatabaseObject view, TableConfiguration? viewConfig)
        {
            // Helper function to add scope key if not already a parameter
            Action<MethodModel> addScopeKeyIfNeeded = (method) =>
            {
                if (classModel.ScopeKeyProperty == null)
                    return;

                method.Parameters.Insert(0, new ParameterModel
                {
                    Name = classModel.ScopeKeyProperty.Name.Camelize(),
                    Type = classModel.ScopeKeyProperty.Type,
                    SourcePropertyName = classModel.ScopeKeyProperty.Name,
                    IsScopeKey = true
                });
            };

            // Helper function to check for duplicate methods before adding
            Action<MethodModel> addMethodIfNotDuplicate = (newMethod) =>
            {
                bool isDuplicate = classModel.Methods.Any(existingMethod =>
                    existingMethod.Name == newMethod.Name &&
                    existingMethod.Parameters.Count == newMethod.Parameters.Count &&
                    existingMethod.Parameters.Select(p => p.Type)
                        .SequenceEqual(newMethod.Parameters.Select(p => p.Type)));

                if (!isDuplicate)
                {
                    classModel.Methods.Add(newMethod);
                }
                else
                {
                    _logger.LogWarning($"Skipping duplicate method generation for '{newMethod.Name}' on class '{classModel.Name}'.");
                }
            };

            // Generate GetAll method
            var getAllMethod = new MethodModel
            {
                Name = "GetAll",
                Type = MethodType.Read,
                ReturnType = $"IEnumerable<{classModel.Name}>"
            };

            // Add scope key parameter if configured
            addScopeKeyIfNeeded(getAllMethod);

            addMethodIfNotDuplicate(getAllMethod);
            _logger.LogMessage($"Generated GetAll method for view {view.FullName}");

            // If read methods are specified in configuration, generate them
            if (viewConfig?.ReadMethods != null && viewConfig.ReadMethods.Any())
            {
                _logger.LogMessage($"Using configured read methods for view {view.FullName}");
                GenerateCustomReadMethods(classModel, viewConfig);
            }
        }

        /// <summary>
        /// Generates read methods based on the indexes defined in the database object.
        /// This is the convention-based approach when no readMethods are specified in configuration.
        /// </summary>
        private void GenerateIndexBasedReadMethods(ClassModel classModel, DatabaseObject dbObject, TableConfiguration? tableConfig)
        {
            // Helper function to check for duplicate methods before adding
            Action<MethodModel> addMethodIfNotDuplicate = (newMethod) =>
            {
                bool isDuplicate = classModel.Methods.Any(existingMethod =>
                    existingMethod.Name == newMethod.Name &&
                    existingMethod.Parameters.Count == newMethod.Parameters.Count &&
                    existingMethod.Parameters.Select(p => p.Type)
                        .SequenceEqual(newMethod.Parameters.Select(p => p.Type)));

                if (!isDuplicate)
                {
                    classModel.Methods.Add(newMethod);
                }
                else
                {
                    _logger.LogWarning($"Skipping duplicate method generation for '{newMethod.Name}' on class '{classModel.Name}'.");
                }
            };

            foreach (var index in dbObject.Indexes)
            {
                // Format the method name based on index columns - now using property names
                var indexProperties = new List<PropertyModel>();
                foreach (var indexColumnName in index.ColumnNames)
                {
                    var property = classModel.Properties.FirstOrDefault(p =>
                        string.Equals(p.SourceColumnName, indexColumnName, StringComparison.OrdinalIgnoreCase));

                    if (property != null)
                    {
                        indexProperties.Add(property);
                    }
                }

                // Skip if we couldn't find properties for the index columns
                if (!indexProperties.Any())
                {
                    _logger.LogWarning($"Skipping index-based method for index {index.Name} on {dbObject.FullName}: no matching properties found.");
                    continue;
                }

                var isUnique = index.IsUnique;
                var methodName = isUnique
                    ? $"Get{classModel.Name}By{string.Join("And", indexProperties.Select(p => p.Name))}"
                    : $"List{classModel.Name.Pluralize()}By{string.Join("And", indexProperties.Select(p => p.Name))}";

                var returnType = isUnique
                    ? classModel.Name
                    : $"IReadOnlyList<{classModel.Name}>";

                var readMethod = new MethodModel
                {
                    Name = methodName,
                    Type = MethodType.Read,
                    ReturnType = returnType
                };

                foreach (var property in indexProperties)
                {
                    readMethod.Parameters.Add(new ParameterModel
                    {
                        Name = property.Name.Camelize(),
                        Type = property.Type,
                        SourcePropertyName = property.Name
                    });
                }

                // Add scope key parameter using helper function
                // Only add if not already part of the index properties
                if (classModel.ScopeKeyProperty != null && !indexProperties.Contains(classModel.ScopeKeyProperty))
                {
                    readMethod.Parameters.Insert(0, new ParameterModel
                    {
                        Name = classModel.ScopeKeyProperty.Name.Camelize(),
                        Type = classModel.ScopeKeyProperty.Type,
                        SourcePropertyName = classModel.ScopeKeyProperty.Name,
                        IsScopeKey = true
                    });
                }

                addMethodIfNotDuplicate(readMethod);
            }
        }

        /// <summary>
        /// Generates custom read methods based on the readMethods specified in the table configuration.
        /// This decouples the application's API from the database's physical structure.
        /// </summary>
        private void GenerateCustomReadMethods(ClassModel classModel, TableConfiguration tableConfig)
        {
            if (tableConfig?.ReadMethods == null || !tableConfig.ReadMethods.Any())
            {
                return;
            }

            // Helper function to check for duplicate methods before adding
            Action<MethodModel> addMethodIfNotDuplicate = (newMethod) =>
            {
                bool isDuplicate = classModel.Methods.Any(existingMethod =>
                    existingMethod.Name == newMethod.Name &&
                    existingMethod.Parameters.Count == newMethod.Parameters.Count &&
                    existingMethod.Parameters.Select(p => p.Type)
                        .SequenceEqual(newMethod.Parameters.Select(p => p.Type)));

                if (!isDuplicate)
                {
                    classModel.Methods.Add(newMethod);
                    _logger.LogMessage($"Generated custom read method: {newMethod.Name}");
                }
                else
                {
                    _logger.LogWarning($"Skipping duplicate method generation for '{newMethod.Name}' on class '{classModel.Name}'.");
                }
            };

            foreach (var readMethodConfig in tableConfig.ReadMethods)
            {
                var method = new MethodModel
                {
                    Name = readMethodConfig.Name,
                    Type = MethodType.Read,
                    ReturnType = $"IEnumerable<{classModel.Name}>"
                };

                foreach (var paramColumn in readMethodConfig.MatchColumns)
                {
                    var property = classModel.Properties.FirstOrDefault(p =>
                        string.Equals(p.SourceColumnName, paramColumn, StringComparison.OrdinalIgnoreCase));

                    if (property != null)
                    {
                        method.Parameters.Add(new ParameterModel
                        {
                            Name = property.Name.Camelize(),
                            Type = property.Type,
                            SourcePropertyName = property.Name
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Could not find property for column '{paramColumn}' in table '{classModel.SourceObjectName}' when creating read method '{readMethodConfig.Name}'.");
                    }
                }

                // Add scope key parameter if configured and not already part of the matchColumns
                var hasMatchColumnWithScopeKey = false;

                if (classModel.ScopeKeyProperty != null && tableConfig.ScopeKey != null)
                {
                    // Check if any of the match columns is the scope key
                    hasMatchColumnWithScopeKey = readMethodConfig.MatchColumns.Any(col =>
                        string.Equals(col, tableConfig.ScopeKey, StringComparison.OrdinalIgnoreCase));

                    // Only add scope key if it's not already in the match columns
                    if (!hasMatchColumnWithScopeKey)
                    {
                        method.Parameters.Insert(0, new ParameterModel
                        {
                            Name = classModel.ScopeKeyProperty.Name.Camelize(),
                            Type = classModel.ScopeKeyProperty.Type,
                            SourcePropertyName = classModel.ScopeKeyProperty.Name,
                            IsScopeKey = true
                        });
                    }
                }

                addMethodIfNotDuplicate(method);
            }
        }

        // Note: This method has been replaced by GenerateCustomReadMethods

        /// <summary>
        /// Resolves the final C# type for a column according to the strict precedence rules:
        /// 1st Priority: Column-specific csharpType override
        /// 2nd Priority: Global type mappings (by priority)
        /// 3rd Priority: Default convention-based mapping
        /// </summary>
        /// <summary>
        /// Resolves the C# type for a database column following the strict precedence rules:
        /// 1. Column-specific override (ultimate override)
        /// 2. Global type mappings (highest priority wins)
        /// 3. Default convention-based mapping
        /// </summary>
        private (string type, bool overridden) ResolveCSharpType(DatabaseColumn column, string schema, string objectName, out PropertySourceInfo sourceInfo)
        {
            // Get the qualified name for table/view lookups
            string qualifiedName = $"{schema}.{objectName}";

            // 1st Priority: Column-specific override (ultimate override)
            if (_configuration.Tables.TryGetValue(qualifiedName, out var tableConfig) &&
                tableConfig.ColumnOverrides != null)
            {
                // Try to get column override
                if (tableConfig.ColumnOverrides.TryGetValue(column.Name, out var columnOverride) &&
                    !string.IsNullOrWhiteSpace(columnOverride.CSharpType))
                {
                    var details = $"Set to '{columnOverride.CSharpType}' by configuration for '{qualifiedName}.{column.Name}'.";
                    sourceInfo = new PropertySourceInfo("C# Type", "Column Override", details);
                    _logger.LogMessage($"Applied column-specific C# type override for {qualifiedName}.{column.Name}: {columnOverride.CSharpType}");
                    return (columnOverride.CSharpType, true);
                }
            }

            // 2nd Priority: Global type mappings (sorted by priority)
            var matchingGlobalRule = FindMatchingGlobalTypeMapping(column, schema, objectName);
            if (matchingGlobalRule != null && !string.IsNullOrWhiteSpace(matchingGlobalRule.Apply?.CSharpType))
            {
                var csharpType = matchingGlobalRule.Apply.CSharpType;
                var details = $"Set to '{csharpType}' by rule with priority {matchingGlobalRule.Priority}. Description: {matchingGlobalRule.Description}";
                sourceInfo = new PropertySourceInfo("C# Type", "Global Type Mapping", details);
                _logger.LogMessage($"Applied global type mapping rule (priority {matchingGlobalRule.Priority}) for {qualifiedName}.{column.Name}: {csharpType}");
                return (csharpType, true);
            }

            // 3rd Priority: Default convention-based mapping
            var sqlTypeString = column.DatabaseType.ToString();
            var defaultType = MapSqlTypeToCSharpType(sqlTypeString, column.IsNullable);
            var sourceDetails = $"Mapped from SQL type '{sqlTypeString}' by default convention.";
            sourceInfo = new PropertySourceInfo("C# Type", "Default Convention", sourceDetails);
            _logger.LogMessage($"Applied default type mapping for {qualifiedName}.{column.Name}: {sqlTypeString} -> {defaultType}");
            return (defaultType, false);
        }

        /// <summary>
        /// Finds the highest priority global type mapping rule that matches the given column.
        /// Returns null if no rules match.
        /// </summary>
        private GlobalTypeMapping FindMatchingGlobalTypeMapping(DatabaseColumn column, string schema, string objectName)
        {
            if (_configuration.GlobalTypeMappings == null || !_configuration.GlobalTypeMappings.Any())
            {
                return null;
            }

            // Find all matching rules and sort by priority (highest first)
            var matchingRules = _configuration.GlobalTypeMappings
                .Where(rule => DoesGlobalRuleMatch(rule, column, schema, objectName))
                .OrderByDescending(rule => rule.Priority)
                .ToList();

            // Return the highest priority rule (first in the list)
            return matchingRules.FirstOrDefault();
        }

        /// <summary>
        /// Determines if a global type mapping rule matches the given column.
        /// ALL conditions in the rule must be met for it to match.
        /// </summary>
        private bool DoesGlobalRuleMatch(GlobalTypeMapping rule, DatabaseColumn column, string schema, string objectName)
        {
            if (rule.Match == null)
                return false;

            // Check schema name regex (if specified)
            if (!string.IsNullOrWhiteSpace(rule.Match.SchemaNameRegex) &&
                !Regex.IsMatch(schema, rule.Match.SchemaNameRegex, RegexOptions.IgnoreCase))
                return false;

            // Check table name regex (if specified)
            if (!string.IsNullOrWhiteSpace(rule.Match.TableNameRegex) &&
                !Regex.IsMatch(objectName, rule.Match.TableNameRegex, RegexOptions.IgnoreCase))
                return false;

            // Check column name regex (if specified)
            if (!string.IsNullOrWhiteSpace(rule.Match.ColumnNameRegex) &&
                !Regex.IsMatch(column.Name, rule.Match.ColumnNameRegex, RegexOptions.IgnoreCase))
                return false;

            // Check SQL type (if specified)
            if (rule.Match.SqlType != null && rule.Match.SqlType.Any() &&
                !DoesSqlTypeMatch(rule.Match.SqlType, column.DatabaseType))
                return false;

            return true;
        }

        /// <summary>
        /// Checks if the column's SQL type matches any of the rule's SQL types.
        /// We need to normalize by removing parameters before comparing.
        /// </summary>
        private bool DoesSqlTypeMatch(List<string> ruleSqlTypes, PwSqlType columnSqlType)
        {
            // Normalize the column SQL type
            var normalizedColumnType = columnSqlType.ToString().Split('(')[0].Trim().ToUpperInvariant();

            // Check if any of the rule's SQL types match the column type
            return ruleSqlTypes.Any(ruleType =>
                string.Equals(normalizedColumnType,
                              ruleType.Split('(')[0].Trim().ToUpperInvariant(),
                              StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Maps SQL types to default C# types following convention-based rules.
        /// This is the fallback when no configuration overrides apply.
        /// </summary>
        private string MapSqlTypeToCSharpType(string sqlType, bool isNullable)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                return "object";

            // Clean up the SQL type (remove size specifications and normalize)
            var cleanSqlType = sqlType.Split('(')[0].Trim().ToLowerInvariant();

            var csharpType = cleanSqlType switch
            {
                "int" => "int",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
                "float" => "double",
                "real" => "float",
                "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" => "string",
                "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "date" => "DateOnly",
                "time" => "TimeOnly",
                "datetimeoffset" => "DateTimeOffset",
                "uniqueidentifier" => "Guid",
                "varbinary" or "binary" or "image" or "rowversion" or "timestamp" => "byte[]",
                "xml" => "string",
                _ => "object" // Unknown type fallback
            };

            // Apply nullability (reference types don't need ? suffix)
            if (isNullable && csharpType != "string" && csharpType != "byte[]" && csharpType != "object")
            {
                csharpType += "?";
            }

            return csharpType;
        }

        /// <summary>
        /// Generates a create input model for a table. This model will be used as the parameter
        /// for the Create method. The model contains all settable properties in the table.
        /// </summary>
        /// <param name="classModel">The class model representing the table</param>
        /// <param name="dbObject">The database object (table)</param>
        /// <param name="tableConfig">Optional configuration overrides for the table</param>
        /// <returns>A CreateInputModel containing all the properties needed for creating a new record</returns>
        private CreateInputModel GenerateCreateInputModel(ClassModel classModel, DatabaseObject dbObject, TableConfiguration? tableConfig)
        {
            _logger.LogMessage($"Generating Create input model for {dbObject.FullName}");

            var createInput = new CreateInputModel
            {
                Name = $"Create{classModel.Name}Input",
                Namespace = classModel.Namespace, // Use the same namespace as the parent entity
                SourceTable = dbObject.FullName
            };

            // Create a set of columns to exclude from the create input
            var excludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // NEW: Exclude the scope key from the create input model
            if (classModel.ScopeKeyProperty != null)
            {
                excludedColumns.Add(classModel.ScopeKeyProperty.SourceColumnName);
                _logger.LogMessage($"  Skipping column {classModel.ScopeKeyProperty.SourceColumnName} for Create input (scope key)");
            }

            // Determine which columns are settable
            // By default, this includes all columns EXCEPT:
            // 1. Identity columns (using heuristics since we don't have explicit IsIdentity property)
            // 2. Computed columns or columns with database-side defaults
            // 3. Special columns like timestamps/rowversions
            foreach (var column in dbObject.Columns)
            {
                // Skip timestamp/rowversion columns
                if (column.DatabaseType.Value.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
                    column.DatabaseType.Value.Equals("rowversion", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogMessage($"  Skipping column {column.Name} for Create input (timestamp/rowversion)");
                    excludedColumns.Add(column.Name);
                    continue;
                }
            }

            // Add all non-excluded columns to the create input model
            foreach (var column in dbObject.Columns)
            {
                if (excludedColumns.Contains(column.Name))
                {
                    continue;
                }

                // Get the matching property from the entity class
                var entityProperty = classModel.Properties.FirstOrDefault(p => p.SourceColumnName == column.Name);
                if (entityProperty == null)
                {
                    _logger.LogMessage($"  Warning: Could not find matching property for column {column.Name} in Create input");
                    continue;
                }

                // Add the property to the create input model
                createInput.Properties.Add(new PropertyModel
                {
                    Name = entityProperty.Name,
                    Type = entityProperty.Type,
                    IsNullable = entityProperty.IsNullable,
                    SourceColumnName = column.Name
                });
            }

            return createInput;
        }
        /// <summary>
        /// Generates an update input model for a table. This model will be used as a parameter
        /// for the Update method. The model contains properties that can be updated, each wrapped
        /// in a Maybe<T> type to distinguish between null values and absence of value.
        /// </summary>
        /// <param name="classModel">The class model representing the table</param>
        /// <param name="dbObject">The database object (table)</param>
        /// <param name="tableConfig">Optional configuration overrides for the table</param>
        /// <returns>An UpdateInputModel containing all the properties that can be updated</returns>
        private UpdateInputModel GenerateUpdateInputModel(ClassModel classModel, DatabaseObject dbObject, TableConfiguration? tableConfig)
        {
            _logger.LogMessage($"Generating Update input model for {dbObject.FullName}");

            var updateInput = new UpdateInputModel
            {
                Name = $"Update{classModel.Name}Input",
                Namespace = classModel.Namespace, // Use the same namespace as the parent entity
                SourceTable = dbObject.FullName
            };

            // Create a set of columns to ignore in the update
            var ignoredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // STEP 1: Default Behavior - Ignore primary key columns
            // This is the core convention: update inputs exclude PK columns by default
            foreach (var pkColumn in dbObject.PrimaryKeyColumns)
            {
                ignoredColumns.Add(pkColumn);
                _logger.LogMessage($"  Ignoring primary key column: {pkColumn}");
            }

            // Also ignore timestamp/rowversion columns as they are system-managed
            foreach (var column in dbObject.Columns)
            {
                if (column.DatabaseType.Value.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
                    column.DatabaseType.Value.Equals("rowversion", StringComparison.OrdinalIgnoreCase))
                {
                    ignoredColumns.Add(column.Name);
                    _logger.LogMessage($"  Ignoring timestamp/rowversion column: {column.Name}");
                }
            }

            // STEP 2: Apply Configuration - Read updateConfig.ignoreColumns
            // This allows the user to specify additional columns to ignore
            if (tableConfig?.UpdateConfig?.IgnoreColumns != null)
            {
                foreach (var columnName in tableConfig.UpdateConfig.IgnoreColumns)
                {
                    ignoredColumns.Add(columnName);
                }

                _logger.LogMessage($"  Additional ignored columns from config: {string.Join(", ", tableConfig.UpdateConfig.IgnoreColumns)}");
            }

            _logger.LogMessage($"  Total ignored columns for update: {string.Join(", ", ignoredColumns)}");

            // STEP 3: Create PropertyModels for each updatable column
            // Add all non-ignored columns to the update input model
            foreach (var column in dbObject.Columns)
            {
                if (ignoredColumns.Contains(column.Name))
                {
                    continue;
                }

                // Get the matching property from the entity class
                var entityProperty = classModel.Properties.FirstOrDefault(p => p.SourceColumnName == column.Name);
                if (entityProperty == null)
                {
                    _logger.LogMessage($"  Warning: Could not find matching property for column {column.Name} in Update input");
                    continue;
                }

                // Add the property to the update input model, wrapped in Maybe<T>
                updateInput.Properties.Add(new PropertyModel
                {
                    Name = entityProperty.Name,
                    // Wrap the type in Maybe<T> to distinguish between null values and absence of value
                    Type = $"Maybe<{entityProperty.Type}>",
                    IsNullable = false, // The Maybe itself is not nullable
                    SourceColumnName = column.Name
                });
            }

            return updateInput;
        }
    }
}