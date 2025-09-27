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

namespace Bravellian.Generators.SqlGen.Pipeline.4_CodeGeneration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Bravellian.Generators.SqlGen.Common.Configuration;
    using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;

/// <summary>
/// Phase 4: Code Generation. This phase is responsible for taking the final,
/// C#-ready model from Phase 3 and rendering it into C# source code files.
/// This class should be as "dumb" as possible, containing no complex decision-making logic.
/// All complex decisions have already been made in Phase 3.
/// </summary>
    public class CSharpCodeGenerator : ICSharpCodeGenerator
{
    private readonly SqlConfiguration configuration;
    private readonly IBvLogger logger;
    private readonly string dbContextTypeName;
    private readonly DbContextGenerator contextGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpCodeGenerator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public CSharpCodeGenerator(SqlConfiguration configuration, IBvLogger logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        this.dbContextTypeName = configuration.DbContextName ?? "DbContext";
        this.contextGenerator = new DbContextGenerator(configuration, logger);
    }

    /// <summary>
    /// Generates C# files from the final C#-ready model.
    /// This method renders the rich model from Phase 3 into file contents.
    /// </summary>
    /// <param name="generationModel">The final C#-ready model from Phase 3.</param>
    /// <returns>A dictionary where keys are file names and values are the generated C# code contents.</returns>
    public Dictionary<string, string> Generate(GenerationModel generationModel)
    {
        this.logger.LogMessage("Phase 4: Generating C# source files...");

        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        var allKnownTypes = generationModel.GetAllGeneratedTypes();

        foreach (var classModel in generationModel.Classes)
        {
            var schemaFolder = classModel.SourceSchemaName;

            // Generate the entity class file (properties only)
            var entityFileName = Path.Combine(schemaFolder, $"{classModel.Name}.g.cs");
            var entityCode = this.GenerateEntityClassCode(classModel, allKnownTypes);
            results.Add(entityFileName, entityCode);
            this.logger.LogMessage($"Generated entity file: {entityFileName}");

            // Generate the repository class file with data access methods (if any)
            if (classModel.Methods.Any())
            {
                var repositoryFileName = Path.Combine(schemaFolder, $"{classModel.Name}Repository.g.cs");
                var repositoryCode = this.GenerateRepositoryClassCode(classModel, allKnownTypes);
                results.Add(repositoryFileName, repositoryCode);
                this.logger.LogMessage($"Generated repository file: {repositoryFileName}");
            }

            // Generate the create input model file (if applicable)
            if (classModel.CreateInput != null)
            {
                var createInputFileName = Path.Combine(schemaFolder, $"{classModel.CreateInput.Name}.g.cs");
                var createInputCode = this.GenerateCreateInputModelCode(classModel.CreateInput);
                results.Add(createInputFileName, createInputCode);
                this.logger.LogMessage($"Generated create input file: {createInputFileName}");
            }

            // Generate the update input model file (if applicable)
            if (classModel.UpdateInput != null)
            {
                var updateInputFileName = Path.Combine(schemaFolder, $"{classModel.UpdateInput.Name}.g.cs");
                var updateInputCode = this.GenerateUpdateInputModelCode(classModel.UpdateInput);
                results.Add(updateInputFileName, updateInputCode);
                this.logger.LogMessage($"Generated update input file: {updateInputFileName}");
            }
        }

        if (this.configuration.GenerateDbContext)
        {
            // Generate the DbContext class
            var dbContextName = this.configuration.DbContextName ?? "ApplicationDbContext";
            var dbContextCode = this.contextGenerator.GenerateDbContext(generationModel.Classes, dbContextName);
            var dbContextFileName = Path.Combine("DbContexts", $"{dbContextName}.g.cs");
            results.Add(dbContextFileName, dbContextCode);
            this.logger.LogMessage($"Generated DbContext file: {dbContextFileName}");
        }

        this.logger.LogMessage($"Phase 4: Generated {results.Count} C# files");
        return results;
    }

    /// <summary>
    /// Writes the generated files to disk.
    /// </summary>
    /// <param name="generatedFiles">Dictionary of file names and their contents.</param>
    /// <param name="outputDirectory">The root directory to write files to.</param>
    public void WriteFilesToDisk(Dictionary<string, string> generatedFiles, string outputDirectory)
    {
        this.logger.LogMessage($"Writing {generatedFiles.Count} files to {outputDirectory}...");

        foreach (var kvp in generatedFiles)
        {
            var filePath = Path.Combine(outputDirectory, kvp.Key);
            try
            {
                var fileInfo = new FileInfo(filePath);

                // Ensure the directory for the file exists.
                fileInfo.Directory?.Create();
                File.WriteAllText(filePath, kvp.Value);
                this.logger.LogMessage($"Wrote {kvp.Key} to disk");
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Failed to write file {filePath}. Error: {ex.Message}");
            }
        }

        this.logger.LogMessage($"Successfully wrote {generatedFiles.Count} files to {outputDirectory}");
    }

    private string GenerateEntityClassCode(ClassModel classModel, IReadOnlyDictionary<string, string> allKnownTypes)
    {
        var propertiesBuilder = new StringBuilder();
        foreach (var property in classModel.Properties)
        {
            var propertyComments = new StringBuilder();
            propertyComments.AppendLine($$"""    /// <summary>""");
            propertyComments.AppendLine($$"""    /// Gets or sets the {{property.Name}} value.""");
            propertyComments.AppendLine($$"""    /// </summary>""");

            // Generate the <remarks> section from the audit trail
            if (property.SourceAuditTrail.Any())
            {
                propertyComments.AppendLine($$"""    /// <remarks>""");
                propertyComments.AppendLine($$"""    /// This property's characteristics were determined by the following rules in order of precedence:""");
                propertyComments.AppendLine($$"""    /// <list type="number">""");

                foreach (var audit in property.SourceAuditTrail)
                {
                    var details = System.Security.SecurityElement.Escape(audit.Details);
                    propertyComments.AppendLine($$"""    /// <item><description><b>{{audit.Aspect}}:</b> Set by <b>{{audit.Source}}</b> - {{details}}</description></item>""");
                }

                propertyComments.AppendLine($$"""    /// </list>""");
                propertyComments.AppendLine($$"""    /// </remarks>""");
            }

            propertiesBuilder.Append(propertyComments);

            var attributes = this.GeneratePropertyAttributes(property);
            if (attributes.Any())
            {
                propertiesBuilder.AppendLine(string.Join(Environment.NewLine, attributes.Select(a => $"    {a}")));
            }

            // Add the PwIndeterminate attribute if the type could not be determined
            if (property.IsIndeterminate)
            {
                propertiesBuilder.AppendLine($$"""    [PwIndeterminate]""");
            }

            string propertyNullableString = (property.IsNullable && !property.Type.EndsWith("?", StringComparison.Ordinal)) ? "?" : string.Empty;
            propertiesBuilder.AppendLine($$"""    public {{property.Type}}{{propertyNullableString}} {{property.Name}} { get; set; }""");
            propertiesBuilder.AppendLine();
        }

        // Remove the last newline if needed
        if (propertiesBuilder.Length > 0)
        {
            propertiesBuilder.Length -= Environment.NewLine.Length;
        }

        var classAttributes = this.GenerateClassAttributes(classModel);

        var configurationClassCode = this.GenerateEntityTypeConfigurationClassCode(classModel);

        if (!string.IsNullOrWhiteSpace(configurationClassCode))
        {
            classAttributes += Environment.NewLine + $"[EntityTypeConfiguration(typeof({classModel.Name}.Configuration))]";
        }

        return $$"""
            // <auto-generated/>
            // CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
            // See NOTICE.md for full restrictions and usage terms.

            #nullable enable

            using System;
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using Bravellian.Database.Shared;

            namespace {{classModel.Namespace}};

            /// <summary>
            /// Represents the {{classModel.SourceObjectName}} {{(classModel.IsView ? "view" : "table")}} from the database.
            /// </summary>
            {{classAttributes}}
            public partial record {{classModel.Name}}
            {
            {{propertiesBuilder}}

            {{configurationClassCode}}
            }
            """;
    }

    private string GenerateRepositoryClassCode(ClassModel classModel, IReadOnlyDictionary<string, string> allKnownTypes)
    {
        var methodsBuilder = new StringBuilder();
        foreach (var method in classModel.Methods)
        {
            methodsBuilder.AppendLine(this.GenerateMethodCode(method, classModel, allKnownTypes));
        }

        // Build the list of using directives
        var usingDirectives = new StringBuilder();
        usingDirectives.AppendLine("using System;");
        usingDirectives.AppendLine("using System.Collections.Generic;");
        usingDirectives.AppendLine("using System.Linq;");
        usingDirectives.AppendLine("using System.Threading;");
        usingDirectives.AppendLine("using System.Threading.Tasks;");
        usingDirectives.AppendLine("using Microsoft.EntityFrameworkCore;");

        // Add reference to Maybe type if we have an UpdateInput model
        if (classModel.UpdateInput != null)
        {
            usingDirectives.AppendLine("using Bravellian;");
        }

        return $$"""
            // <auto-generated/>
            // CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
            // See NOTICE.md for full restrictions and usage terms.

            #nullable enable

            {{usingDirectives}}
            namespace {{classModel.Namespace}};
            
            /// <summary>
            /// Provides extension methods for accessing {{classModel.Name}} data in the database.
            /// </summary>
            public static class {{classModel.Name}}Repository
            {
            {{methodsBuilder}}
            }
            
            """;
    }

    private string GenerateMethodCode(MethodModel method, ClassModel classModel, IReadOnlyDictionary<string, string> allKnownTypes)
    {
        // Helper to get fully qualified type name
        Func<string, string> getFqdn = (typeName) =>
        {
            var cleanType = typeName.Replace("?", string.Empty).Replace("IEnumerable<", string.Empty).Replace(">", string.Empty);

            // Prioritize the current class's own type to avoid ambiguity with same-named classes in other schemas.
            if (string.Equals(cleanType, classModel.Name, StringComparison.Ordinal))
            {
                return typeName.Replace(cleanType, $"{classModel.Namespace}.{classModel.Name}");
            }

            // Also check for associated input models within the same namespace context.
            if (classModel.CreateInput != null && string.Equals(cleanType, classModel.CreateInput.Name, StringComparison.Ordinal))
            {
                return typeName.Replace(cleanType, $"{classModel.Namespace}.{classModel.CreateInput.Name}");
            }

            if (classModel.UpdateInput != null && string.Equals(cleanType, classModel.UpdateInput.Name, StringComparison.Ordinal))
            {
                return typeName.Replace(cleanType, $"{classModel.Namespace}.{classModel.UpdateInput.Name}");
            }

            return allKnownTypes.TryGetValue(cleanType, out var fqdn) ? typeName.Replace(cleanType, fqdn) : typeName;
        };

        // Determine async return types
        string returnType;
        var fqdnReturnType = getFqdn(method.ReturnType);

        if (string.Equals(method.ReturnType, classModel.Name, StringComparison.Ordinal))
        {
            returnType = $"System.Threading.Tasks.Task<{fqdnReturnType}?>";
        }
        else if (method.ReturnType.Contains("IEnumerable"))
        {
            var innerType = method.ReturnType.Replace("IEnumerable<", string.Empty).Replace(">", string.Empty);
            var fqdnInnerType = getFqdn(innerType);
            returnType = $"System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<{fqdnInnerType}>>";
        }
        else if (string.Equals(method.ReturnType, "void", StringComparison.Ordinal))
        {
            returnType = "System.Threading.Tasks.Task";
        }
        else
        {
            returnType = $"System.Threading.Tasks.Task<{fqdnReturnType}>";
        }

        var methodName = $"{method.Name}Async";

        // Build parameter string
        var methodParams = method.Parameters.Select(p => $"{getFqdn(p.Type)} {p.Name}");
        var allParams = string.Join(", ", methodParams);
        var fullParams = $"this {this.dbContextTypeName} context{(string.IsNullOrEmpty(allParams) ? string.Empty : $", {allParams}")}, System.Threading.CancellationToken cancellationToken = default";

        // Generate implementation
        var implementation = this.GenerateMethodImplementation(method, classModel, allKnownTypes);

        // Generate XML comments
        var comments = this.GenerateMethodComments(method, classModel, returnType);

        return $$"""
        {{comments}}
            public static async {{returnType}} {{methodName}}({{fullParams}})
            {
        {{implementation}}
            }
        """;
    }

    private string GenerateMethodComments(MethodModel method, ClassModel classModel, string returnType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Asynchronously {this.GetMethodActionDescription(method, classModel)}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"context\">The database context.</param>");

        foreach (var parameter in method.Parameters)
        {
            // Special handling for Create and Update input parameters
            if (method.Type == MethodType.Create && string.Equals(parameter.Name, "input", StringComparison.Ordinal))
            {
                sb.AppendLine($"    /// <param name=\"{parameter.Name}\">The input model containing values for creating a new {classModel.Name}.</param>");
            }
            else if (method.Type == MethodType.Update && string.Equals(parameter.Name, "input", StringComparison.Ordinal))
            {
                sb.AppendLine($"    /// <param name=\"{parameter.Name}\">The input model containing values to update. Only properties with HasValue=true will be updated.</param>");
            }
            else
            {
                sb.AppendLine($"    /// <param name=\"{parameter.Name}\">The value for the {parameter.SourcePropertyName} column.</param>");
            }
        }

        sb.AppendLine($"    /// <param name=\"cancellationToken\">The cancellation token.</param>");

        if (!returnType.StartsWith("Task", StringComparison.Ordinal) || returnType.Contains("<"))
        {
            sb.AppendLine($"    /// <returns>A task that represents the asynchronous operation. The task result contains the generated data.</returns>");
        }
        else
        {
            sb.AppendLine($"    /// <returns>A task that represents the asynchronous operation.</returns>");
        }

        return sb.ToString();
    }

    private string GetMethodActionDescription(MethodModel method, ClassModel classModel)
    {
        var target = classModel.Name;
        return method.Type switch
        {
            MethodType.Read => $"retrieves {(method.ReturnType.Contains("IEnumerable") ? "a list of" : "a single")} {target} record(s)",
            MethodType.Create => $"creates a new {target} record",
            MethodType.Update => $"updates an existing {target} record",
            MethodType.Delete => $"deletes a {target} record",
            _ => $"executes a custom operation on {target}"
        };
    }

    private string GenerateMethodImplementation(MethodModel method, ClassModel classModel, IReadOnlyDictionary<string, string> allKnownTypes)
    {
        // Helper to get fully qualified type name
        Func<string, string> getFqdn = (typeName) =>
        {
            var cleanType = typeName.Replace("?", string.Empty);

            // Prioritize the current class's own type to avoid ambiguity with same-named classes in other schemas.
            if (string.Equals(cleanType, classModel.Name, StringComparison.Ordinal))
            {
                return $"{classModel.Namespace}.{classModel.Name}";
            }

            return allKnownTypes.TryGetValue(cleanType, out var fqdn) ? fqdn : cleanType;
        };
        var fqdnClassName = getFqdn(classModel.Name);

        // Get scope key property and parameter
        var scopeKeyProp = classModel.ScopeKeyProperty;
        var scopeKeyParam = scopeKeyProp != null
            ? method.Parameters.FirstOrDefault(p => string.Equals(p.SourcePropertyName, scopeKeyProp.Name, StringComparison.OrdinalIgnoreCase))
            : null;

        // Build the WHERE clause, starting with regular parameters
        var whereClauses = new List<string>();

        // Add all parameters to the WHERE clause
        foreach (var param in method.Parameters)
        {
            // Skip the input parameter which is used for Create/Update
            if (string.Equals(param.Name, "input", StringComparison.Ordinal))
            {
                continue;
            }

            whereClauses.Add($"x.{param.SourcePropertyName} == {param.Name}");
        }

        // Build the WHERE predicate string
        var wherePredicate = string.Join(" && ", whereClauses);

        switch (method.Type)
        {
            case MethodType.Read:
                if (string.IsNullOrWhiteSpace(wherePredicate))
                {
                    // Handle cases with no WHERE clause (e.g., GetAll)
                    return $"        return await context.Set<{fqdnClassName}>().AsNoTracking().ToListAsync(cancellationToken);";
                }
                else if (method.ReturnType.Contains("IEnumerable") || method.ReturnType.Contains("IReadOnlyList"))
                {
                    return $"        return await context.Set<{fqdnClassName}>().AsNoTracking().Where(x => {wherePredicate}).ToListAsync(cancellationToken);";
                }
                else
                {
                    return $"        return await context.Set<{fqdnClassName}>().AsNoTracking().SingleOrDefaultAsync(x => {wherePredicate}, cancellationToken);";
                }

            case MethodType.Create:
                // For Create, map from the input model to the entity
                if (classModel.CreateInput?.Properties == null)
                {
                    // Fallback if for some reason the input model is not available
                    return $"        throw new NotImplementedException(\"Create method implementation not available.\");";
                }

                var createEntityProps = classModel.CreateInput.Properties.Select(p => p.Name);
                var createAssignments = string.Join(",\n", createEntityProps.Select(prop => $"            {prop} = input.{prop}"));

                // Add scope key assignment if scope key parameter exists
                string scopeKeyAssignment = string.Empty;
                if (scopeKeyParam != null && scopeKeyProp != null)
                {
                    // The scope key should be assigned from the parameter, not from the input
                    // since it's excluded from the CreateInput model
                    scopeKeyAssignment = $",\n            {scopeKeyProp.Name} = {scopeKeyParam.Name}";
                }

                // Determine the return statement based on the presence of primary keys
                // string returnStatement;
                // var pkProperty = classModel.PrimaryKeyProperties.FirstOrDefault();
                // if (pkProperty != null && pkProperty.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                // {
                //     returnStatement = "return entity.Id;";
                // }
                // else
                // {
                //     returnStatement = "return entity;";
                // }
                return $$"""
                    // Map properties from the input model to a new entity
                    var entity = new {{fqdnClassName}}
                    {
            {{createAssignments}}{{scopeKeyAssignment}}
                    };
                    
                    // Add the entity to the context and save changes
                    context.Set<{{fqdnClassName}}>().Add(entity);
                    await context.SaveChangesAsync(cancellationToken);
                    return entity;
            """;

            case MethodType.Update:
                // For Update, check each Maybe<T> property in the input model
                if (classModel.UpdateInput?.Properties == null)
                {
                    // Fallback if for some reason the input model is not available
                    return $"        throw new NotImplementedException(\"Update method implementation not available.\");";
                }

                // We're using the wherePredicate directly now, which is built from all parameters
                // including the scope key if present
                var updateProperties = classModel.UpdateInput.Properties.Select(p => p.Name);
                var updateAssignments = updateProperties.Select(prop =>
                    $"        // Check if the {prop} property was included in the update request\n" +
                    $"        if (input.{prop}.HasValue)\n" +
                    $"        {{\n" +
                    $"            // Update the property with the new value (which might be null)\n" +
                    $"            entity.{prop} = input.{prop}.Value;\n" +
                    $"        }}");

                return $$"""
                    // Find the entity to update using primary key(s) and scope key (if present)
                    var entity = await context.Set<{{fqdnClassName}}>().AsTracking().SingleOrDefaultAsync(x => {{wherePredicate}}, cancellationToken);
                    if (entity == null)
                    {
                        // Entity not found, return null to indicate no update was performed.
                        return null;
                    }
                    
                    // Only update properties that were explicitly included in the input model
            {{string.Join("\n\n", updateAssignments)}}
                    
                    // Save changes to the database
                    await context.SaveChangesAsync(cancellationToken);
                    return entity;
            """;

            case MethodType.Delete:
                // For Delete, we now have two implementation options:
                // 1. Use a direct WHERE clause when we have a scope key (more efficient)
                // 2. Use the attach-and-remove approach when no scope key is present
                if (scopeKeyParam != null)
                {
                    // More efficient implementation that uses a WHERE clause with all parameters including scope key
                    return $$"""
                        // Delete the entity matching the primary key(s) and scope key
                        var entity = await context.Set<{{fqdnClassName}}>()
                            .AsTracking()
                            .SingleOrDefaultAsync(x => {{wherePredicate}}, cancellationToken);
                            
                        if (entity != null)
                        {
                            context.Set<{{fqdnClassName}}>().Remove(entity);
                            await context.SaveChangesAsync(cancellationToken);
                        }
                """;
                }
                else
                {
                    // Build property assignments for Delete
                    var propertyAssignments = method.Parameters.Select(p => $"{p.SourcePropertyName} = {p.Name}");

                    return $$"""
                        var entity = new {{fqdnClassName}} { {{string.Join(", ", propertyAssignments)}} };
                        context.Set<{{fqdnClassName}}>().Attach(entity);
                        context.Set<{{fqdnClassName}}>().Remove(entity);
                        await context.SaveChangesAsync(cancellationToken);
                """;
                }

            default:
                return "        // Custom method implementation logic would go here.\n            throw new NotImplementedException();";
        }
    }

    private string GenerateClassAttributes(ClassModel classModel)
    {
        var attributes = new List<string>();

        // [Table("blogs", Schema = "blogging")]
        attributes.Add($"[Table(\"{classModel.SourceObjectName}\", Schema = \"{classModel.SourceSchemaName}\")]");

        // [PrimaryKey(nameof(State), nameof(LicensePlate))]
        if (classModel.PrimaryKeyProperties.Any())
        {
            var pkParams = string.Join(", ", classModel.PrimaryKeyProperties.Select(p => $"nameof({p.Name})"));
            attributes.Add($"[PrimaryKey({pkParams})]");
        }
        else
        {
            attributes.Add($"[Keyless]");
        }

        // [Index(nameof(FirstName), nameof(LastName), IsUnique = true)]
        foreach (var index in classModel.Indexes)
        {
            var indexCols = classModel.Properties
                .Where(p => index.Columns.Contains(p.SourceColumnName, StringComparer.Ordinal))
                .Select(p => $"nameof({p.Name})");

            if (indexCols.Any())
            {
                var attributeParams = new List<string>
                    {
                        string.Join(", ", indexCols),
                    };

                if (index.IsUnique)
                {
                    attributeParams.Add("IsUnique = true");
                }

                if (!string.IsNullOrEmpty(index.Name))
                {
                    attributeParams.Add($"Name = \"{index.Name}\"");
                }

                attributes.Add($"[Index({string.Join(", ", attributeParams)})]");
            }
        }

        return string.Join(Environment.NewLine, attributes);
    }

    private List<string> GeneratePropertyAttributes(PropertyModel property)
    {
        var attributes = new List<string>();
        var columnParams = new List<string>();

        // [Column("source_column_name")]
        if (!string.Equals(property.Name, property.SourceColumnName, StringComparison.Ordinal))
        {
            columnParams.Add($"\"{property.SourceColumnName}\"");
        }

        // [Column(TypeName = "varchar(200)")]
        columnParams.Add($"TypeName = \"{property.SourceSqlType}\"");

        if (columnParams.Any())
        {
            attributes.Add($"[Column({string.Join(", ", columnParams)})]");
        }

        // [Required] for non-nullable reference types
        if (!property.IsNullable && (string.Equals(property.Type, "string", StringComparison.Ordinal) || string.Equals(property.Type, "byte[]", StringComparison.Ordinal)))
        {
            attributes.Add("[Required]");
        }

        // [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        if (property.IsComputed)
        {
            attributes.Add("[DatabaseGenerated(DatabaseGeneratedOption.Computed)]");
        }

        return attributes;
    }

    /// <summary>
    /// Generates the C# code for a create input model class.
    /// </summary>
    /// <param name="model">The create input model.</param>
    /// <returns>The C# code as a string.</returns>
    private string GenerateCreateInputModelCode(CreateInputModel? model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "CreateInput model is null - this should not happen as it should be created in Phase 3");
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.");
        sb.AppendLine("// See NOTICE.md for full restrictions and usage terms.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Input model for creating a new {model.Name.Replace("Create", string.Empty).Replace("Input", string.Empty)}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed partial record {model.Name}");
        sb.AppendLine("{");

        // Generate properties
        foreach (var property in model.Properties)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Gets or sets the {property.Name}.");
            sb.AppendLine("    /// </summary>");
            string propertyNullableString = (property.IsNullable && !property.Type.EndsWith("?", StringComparison.Ordinal)) ? "?" : string.Empty;
            sb.AppendLine($"    public {property.Type}{propertyNullableString} {property.Name} {{ get; init; }}");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the C# code for an update input model class.
    /// </summary>
    /// <param name="model">The update input model.</param>
    /// <returns>The C# code as a string.</returns>
    private string GenerateUpdateInputModelCode(UpdateInputModel? model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "UpdateInput model is null - this should not happen as it should be created in Phase 3");
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.");
        sb.AppendLine("// See NOTICE.md for full restrictions and usage terms.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using Bravellian;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Input model for updating an existing {model.Name.Replace("Update", string.Empty).Replace("Input", string.Empty)}.");
        sb.AppendLine("/// Properties wrapped in Maybe<T> to distinguish between null values and absence of value.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed partial record {model.Name}");
        sb.AppendLine("{");

        // Generate properties
        foreach (var property in model.Properties)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Gets or sets the {property.Name}.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public {property.Type} {property.Name} {{ get; init; }}");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the nested IEntityTypeConfiguration class for an entity.
    /// </summary>
    private string GenerateEntityTypeConfigurationClassCode(ClassModel classModel)
    {
        var conversions = new StringBuilder();

        foreach (var property in classModel.Properties)
        {
            // Your rule: A custom type was explicitly set, overriding the default.
            if (this.PropertyNeedsValueConverter(property))
            {
                var providerType = this.GetProviderTypeFromSqlType(property.SourceSqlType);
                if (providerType == null)
                {
                    // If we can't map the SQL type, we can't create a converter.
                    this.logger.LogWarning($"Could not determine provider type for property '{property.Name}' with SQL type '{property.SourceSqlType}'. Skipping value converter generation.");
                    continue;
                }

                // This generates the builder.Property(p => p.Name).HasConversion(...) call
                conversions.AppendLine($$"""
                            builder.Property(e => e.{{property.Name}})
                                .HasConversion(new TypeConverterValueConverter<{{property.Type}}, {{providerType}}>());
                    """);
            }
        }

        // If no properties needed conversion, we can return an empty string.
        if (conversions.Length == 0)
        {
            return string.Empty;
        }

        return $$"""

            /// <summary>
            /// Provides Entity Framework Core configuration for the <see cref="{{classModel.Name}}"/> entity.
            /// </summary>
            public class Configuration : IEntityTypeConfiguration<{{classModel.Name}}>
            {
                /// <summary>
                /// Configures the entity of type <see cref="{{classModel.Name}}"/>.
                /// </summary>
                /// <param name="builder">The builder to be used to configure the entity type.</param>
                public void Configure(EntityTypeBuilder<{{classModel.Name}}> builder)
                {
        {{conversions}}
                }
            }
        """;
    }

    /// <summary>
    /// Determines if a property needs a value converter based on its audit trail.
    /// </summary>
    private bool PropertyNeedsValueConverter(PropertyModel property)
    {
        return property.IsTypeOverridden;
    }

    /// <summary>
    /// Maps a SQL Server data type name to its corresponding C# primitive type for use as a ValueConverter provider type.
    /// </summary>
    private string? GetProviderTypeFromSqlType(PwSqlType sqlType)
    {
        // This mapping is crucial for the TypeConverterValueConverter<TModel, TProvider>
        return sqlType.Value.ToUpperInvariant() switch
        {
            "BIGINT" => "long",
            "INT" => "int",
            "SMALLINT" => "short",
            "TINYINT" => "byte",
            "BIT" => "bool",
            "DECIMAL" or "NUMERIC" or "MONEY" or "SMALLMONEY" => "decimal",
            "FLOAT" => "double",
            "REAL" => "float",

            "VARCHAR" or "NVARCHAR" or "CHAR" or "NCHAR" or "TEXT" or "NTEXT" => "string",

            "DATE" or "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "DateTime",
            "DATETIMEOFFSET" => "DateTimeOffset",
            "TIME" => "TimeSpan",

            "UNIQUEIDENTIFIER" => "Guid",

            "VARBINARY" or "BINARY" or "IMAGE" => "byte[]",

            _ => null // Return null if we don't have a mapping
        };
    }
}
}
