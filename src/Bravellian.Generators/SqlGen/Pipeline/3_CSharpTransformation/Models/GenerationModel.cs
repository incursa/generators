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

namespace Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models
{
    /// <summary>
    /// Represents the final C# model ready for code generation
    /// </summary>
    public class GenerationModel
    {
        /// <summary>
        /// Gets or sets the C# classes to generate
        /// </summary>
        public List<ClassModel> Classes { get; set; } = new();

        /// <summary>
        /// Scans all class and input models to build a dictionary mapping simple type names
        /// to their fully qualified names. This is used by the code generator to resolve
        /// type references correctly.
        /// </summary>
        /// <returns>A read-only dictionary of all generated types.</returns>
        public IReadOnlyDictionary<string, string> GetAllGeneratedTypes()
        {
            var allTypes = new Dictionary<string, string>();

            foreach (var classModel in Classes)
            {
                // Add the main entity class
                if (!allTypes.ContainsKey(classModel.Name))
                {
                    allTypes.Add(classModel.Name, $"{classModel.Namespace}.{classModel.Name}");
                }

                // Add the CreateInput model if it exists
                if (classModel.CreateInput != null &&
                    !allTypes.ContainsKey(classModel.CreateInput.Name))
                {
                    allTypes.Add(classModel.CreateInput.Name, $"{classModel.CreateInput.Namespace}.{classModel.CreateInput.Name}");
                }

                // Add the UpdateInput model if it exists
                if (classModel.UpdateInput != null &&
                    !allTypes.ContainsKey(classModel.UpdateInput.Name))
                {
                    allTypes.Add(classModel.UpdateInput.Name, $"{classModel.UpdateInput.Namespace}.{classModel.UpdateInput.Name}");
                }
            }

            return allTypes;
        }
    }

    /// <summary>
    /// Represents a C# class to generate
    /// </summary>
    public class ClassModel
    {
        /// <summary>
        /// Gets or sets the C# class name for the model.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the base C# class name, without any schema prefixes.
        /// This is used for generating file names and related input model names.
        /// </summary>
        public string BaseName { get; set; }

        /// <summary>
        /// Gets or sets the C# namespace for the class.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this class represents a view
        /// </summary>
        public bool IsView { get; set; }

        /// <summary>
        /// Gets or sets the properties for the class
        /// </summary>
        public List<PropertyModel> Properties { get; set; } = new List<PropertyModel>();

        /// <summary>
        /// Gets or sets the data access methods for the class
        /// </summary>
        public List<MethodModel> Methods { get; set; } = new List<MethodModel>();

        /// <summary>
        /// Gets or sets the primary key properties for the class.
        /// This provides easy access to primary key properties for method generation.
        /// </summary>
        public List<PropertyModel> PrimaryKeyProperties { get; set; } = new List<PropertyModel>();

        /// <summary>
        /// Gets or sets the list of database indexes for this class's source table.
        /// </summary>
        public List<IndexModel> Indexes { get; set; } = new List<IndexModel>();

        /// <summary>
        /// Gets or sets the source database object name
        /// </summary>
        public string SourceObjectName { get; set; }

        /// <summary>
        /// Gets or sets the source database schema name
        /// </summary>
        public string SourceSchemaName { get; set; }

        /// <summary>
        /// Gets or sets the create input model for this class.
        /// This will be null for view classes.
        /// </summary>
        public CreateInputModel CreateInput { get; set; }

        /// <summary>
        /// Gets or sets the update input model for this class.
        /// This will be null for view classes.
        /// </summary>
        public UpdateInputModel UpdateInput { get; set; }
        
        /// <summary>
        /// Gets or sets the property that corresponds to the configured scope key.
        /// This property is used for multi-tenant/scoping functionality.
        /// </summary>
        public PropertyModel? ScopeKeyProperty { get; set; }
    }

    /// <summary>
    /// Represents a C# property to generate
    /// </summary>
    public class PropertyModel
    {
        /// <summary>
        /// Gets or sets the property name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the property type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this property is nullable
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this property is part of the primary key
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Gets or sets the source column name from the database.
        /// </summary>
        public string SourceColumnName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the column's type could not be determined.
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets the audit trail for how this property's characteristics were determined.
        /// </summary>
        public List<PropertySourceInfo> SourceAuditTrail { get; } = new List<PropertySourceInfo>();
        public bool IsTypeOverridden { get; set; }
        public bool IsComputed { get; internal set; }
        public PwSqlType SourceSqlType { get; internal set; }
    }


    /// <summary>
    /// Represents a database index in the C# transformation model.
    /// </summary>
    public class IndexModel
    {
        /// <summary>
        /// Gets or sets the name of the index.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the index is unique.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets the list of column names included in the index.
        /// </summary>
        public List<string> Columns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a data access method to generate
    /// </summary>
    public class MethodModel
    {
        /// <summary>
        /// Gets or sets the method name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the method type (Read, Create, Update, Delete)
        /// </summary>
        public MethodType Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this method operates on the primary key
        /// </summary>
        public bool IsPrimaryKeyMethod { get; set; }

        /// <summary>
        /// Gets or sets the method parameters
        /// </summary>
        public List<ParameterModel> Parameters { get; set; } = new List<ParameterModel>();

        /// <summary>
        /// Gets or sets the SQL statement for the method
        /// </summary>
        public string SqlStatement { get; set; }

        /// <summary>
        /// Gets or sets the method return type
        /// </summary>
        public string ReturnType { get; set; }
        
        /// <summary>
        /// Gets additional metadata for the method. This can be used to store method-specific
        /// information that will be used during code generation, such as ignored columns for updates.
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a method parameter
    /// </summary>
    public class ParameterModel
    {
        /// <summary>
        /// Gets or sets the parameter name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the parameter type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the source property name
        /// </summary>
        public string SourcePropertyName { get; set; }

        /// <summary>
        /// Gets or sets whether this parameter is a scope key used for multi-tenancy
        /// </summary>
        public bool IsScopeKey { get; set; }
    }

    /// <summary>
    /// Method types
    /// </summary>
    public enum MethodType
    {
        /// <summary>
        /// Read operation
        /// </summary>
        Read,

        /// <summary>
        /// Create operation
        /// </summary>
        Create,

        /// <summary>
        /// Update operation
        /// </summary>
        Update,

        /// <summary>
        /// Delete operation
        /// </summary>
        Delete
    }

    /// <summary>
    /// Represents a create input model class to generate
    /// </summary>
    public class CreateInputModel
    {
        /// <summary>
        /// Gets or sets the class name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace for the class
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the properties for the create input class
        /// </summary>
        public List<PropertyModel> Properties { get; set; } = new List<PropertyModel>();

        /// <summary>
        /// Gets or sets the source table for which this input is created
        /// </summary>
        public string SourceTable { get; set; }
    }

    /// <summary>
    /// Represents an update input model class to generate
    /// </summary>
    public class UpdateInputModel
    {
        /// <summary>
        /// Gets or sets the class name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the namespace for the class
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the properties for the update input class
        /// The Type for each property will be wrapped (e.g., Maybe<string>)
        /// </summary>
        public List<PropertyModel> Properties { get; set; } = new List<PropertyModel>();

        /// <summary>
        /// Gets or sets the source table for which this input is created
        /// </summary>
        public string SourceTable { get; set; }
    }
}
