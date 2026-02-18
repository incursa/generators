namespace Incursa.Generators;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.CodeAnalysis;

[Generator(LanguageNames.CSharp)]
public sealed class DtoEntitySourceGenerator : IIncrementalGenerator
{
    private static readonly string[] CandidateSuffixes = new[]
    {
        ".dto.json",
        ".entity.json",
    };

    private readonly record struct InputFile
    {
        public string Path { get; }
        public string? Content { get; }

        public InputFile(string path, string? content)
        {
            Path = path;
            Content = content;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get license header from MSBuild property
        var licenseHeaderProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.GeneratedCodeLicenseHeader", out var header);
                return header ?? string.Empty;
            });

        var candidateFiles = context.AdditionalTextsProvider
            .Where(static text => IsCandidateFile(text.Path))
            .Select(static (text, cancellationToken) => new InputFile(text.Path, text.GetText(cancellationToken)?.ToString()))
            .Where(static input => !string.IsNullOrWhiteSpace(input.Content));

        // Combine files with license header
        var filesWithLicense = candidateFiles.Combine(licenseHeaderProvider);

        context.RegisterSourceOutput(filesWithLicense, static (productionContext, input) =>
        {
            var (file, licenseHeader) = input;
            try
            {
                var generated = Generate(file.Path, file.Content!, licenseHeader, productionContext.CancellationToken, productionContext);
                if (generated == null || !generated.Any())
                {
                    GeneratorDiagnostics.ReportSkipped(productionContext, $"No output generated for '{file.Path}'. Ensure required DTO elements or JSON fields are present.");
                    return;
                }

                var addedHintNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (fileName, source) in generated)
                {
                    productionContext.CancellationToken.ThrowIfCancellationRequested();
                    if (!addedHintNames.Add(fileName))
                    {
                        GeneratorDiagnostics.ReportDuplicateHintName(productionContext, fileName);
                        continue;
                    }
                    productionContext.AddSource(fileName, source);
                }
            }
            catch (Exception ex)
            {
                GeneratorDiagnostics.ReportError(productionContext, $"DtoEntitySourceGenerator failed for '{file.Path}'", ex);
            }
        });
    }

    /// <summary>
    /// Public wrapper for CLI usage
    /// </summary>
    public IEnumerable<(string fileName, string source)>? GenerateFromFiles(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        return Generate(filePath, fileContent, string.Empty, cancellationToken, null);
    }

    private static bool IsCandidateFile(string path)
    {
        for (var i = 0; i < CandidateSuffixes.Length; i++)
        {
            if (path.EndsWith(CandidateSuffixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string fileName, string source)>? Generate(string filePath, string fileContent, string licenseHeader, CancellationToken cancellationToken, SourceProductionContext? productionContext)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GenerateFromJson(fileContent, filePath, licenseHeader, cancellationToken, productionContext);
    }

    private static IEnumerable<(string fileName, string source)>? GenerateFromJson(string fileContent, string filePath, string licenseHeader, CancellationToken cancellationToken, SourceProductionContext? productionContext)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(fileContent);
            var root = jsonDoc.RootElement;

            var genParams = ParseGeneratorParamsFromJson(root, filePath, licenseHeader, cancellationToken, parentNamespace: null, productionContext);
            if (genParams == null)
            {
                return null;
            }

            List<(string fileName, string source)> generated = new();
            GenerateCodeRecursive(genParams, generated, cancellationToken);

            return generated;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DtoEntityGenerator.GeneratorParams? ParseGeneratorParamsFromJson(JsonElement root, string sourceFilePath, string licenseHeader, CancellationToken cancellationToken, string? parentNamespace, SourceProductionContext? productionContext)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!root.TryGetProperty("name", out var nameElement) ||
            (!root.TryGetProperty("namespace", out var namespaceElement) && string.IsNullOrEmpty(parentNamespace)))
        {
            return null;
        }

        var name = nameElement.GetString();
        var ns = root.TryGetProperty("namespace", out var nsElement) ? nsElement.GetString() : parentNamespace;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ns))
        {
            return null;
        }

        var documentation = root.TryGetProperty("documentation", out var docElement) ? docElement.GetString() : null;
        var inherits = root.TryGetProperty("inherits", out var inheritsElement) ? inheritsElement.GetString() : null;
        var accessibility = root.TryGetProperty("accessibility", out var accessibilityElement) ? accessibilityElement.GetString() : "public";
        var isAbstract = root.TryGetProperty("abstract", out var abstractElement) && abstractElement.GetBoolean();
        var classOnly = root.TryGetProperty("classOnly", out var classOnlyElement) && classOnlyElement.GetBoolean();
        var isStrict = root.TryGetProperty("strict", out var strictElement) && strictElement.GetBoolean();
        var useParentValidator = !root.TryGetProperty("useParentValidator", out var useParentValidatorElement) || useParentValidatorElement.GetBoolean();
        var noCreateMethod = root.TryGetProperty("noCreateMethod", out var noCreateMethodElement) && noCreateMethodElement.GetBoolean();
        var isRecordStruct = root.TryGetProperty("isRecordStruct", out var isRecordStructElement) && isRecordStructElement.GetBoolean();

        if (isAbstract && isStrict)
        {
            return null;
        }

        if (isRecordStruct)
        {
            if (isAbstract)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(inherits))
            {
                // Records cannot inherit from classes
                return null;
            }
        }

        var properties = new List<DtoEntityGenerator.PropertyDescriptor>();
        if (root.TryGetProperty("properties", out var propsElement) && propsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!prop.TryGetProperty("name", out var propNameElement) || !prop.TryGetProperty("type", out var propTypeElement))
                {
                    continue;
                }

                var propName = propNameElement.GetString();
                var propType = propTypeElement.GetString();

                if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propType))
                {
                    continue;
                }

                var propDocumentation = prop.TryGetProperty("documentation", out var propDocElement) ? propDocElement.GetString() : null;
                var requiredSpecified = prop.TryGetProperty("required", out var requiredElement);
                var isRequired = requiredSpecified ? requiredElement.GetBoolean() : true;
                var isNullable = prop.TryGetProperty("nullable", out var nullableElement) && nullableElement.GetBoolean();
                var max = prop.TryGetProperty("max", out var maxElement) ? maxElement.GetString() : null;
                var min = prop.TryGetProperty("min", out var minElement) ? minElement.GetString() : null;
                var regex = prop.TryGetProperty("regex", out var regexElement) ? regexElement.GetString() : null;
                var jsonProperty = prop.TryGetProperty("jsonProperty", out var jsonPropertyElement) ? jsonPropertyElement.GetString() : null;
                var noDefault = prop.TryGetProperty("noDefault", out var noDefaultElement) && noDefaultElement.GetBoolean();
                var isSettable = prop.TryGetProperty("settable", out var settableElement) && settableElement.GetBoolean();
                var expression = prop.TryGetProperty("expression", out var expressionElement) ? expressionElement.GetString() : null;
                var defaultValue = prop.TryGetProperty("defaultValue", out var defaultValueElement) ? defaultValueElement.GetString() : null;
                var hasDefaultValue = !string.IsNullOrEmpty(defaultValue) && !noDefault;

                if (!requiredSpecified && hasDefaultValue)
                {
                    isRequired = false;
                }

                // Validate property configuration (skip validation for expression properties)
                if (string.IsNullOrEmpty(expression))
                {
                    var validationResult = ValidatePropertyConfiguration(propName, propType, isRequired, isNullable, hasDefaultValue, isSettable, isStrict);
                    if (validationResult != null && productionContext.HasValue)
                    {
                        var (isError, message) = validationResult.Value;
                        if (isError)
                        {
                            GeneratorDiagnostics.ReportValidationError(productionContext.Value, propName, message, sourceFilePath);
                        }
                        else
                        {
                            GeneratorDiagnostics.ReportValidationWarning(productionContext.Value, propName, message, sourceFilePath);
                        }
                    }
                }

                properties.Add(new DtoEntityGenerator.PropertyDescriptor(
                    propName, propType, isRequired, isNullable, max, min, regex, jsonProperty, noDefault, isSettable, expression, propDocumentation, defaultValue));
            }
        }

        var nestedEntities = new List<DtoEntityGenerator.GeneratorParams>();
        if (root.TryGetProperty("nestedEntities", out var nestedElement) && nestedElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var nested in nestedElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nestedParams = ParseGeneratorParamsFromJson(nested, sourceFilePath, licenseHeader, cancellationToken, ns, productionContext);
                if (nestedParams != null)
                {
                    nestedEntities.Add(nestedParams);
                }
            }
        }

        var genParams = new DtoEntityGenerator.GeneratorParams(
            name: name,
            ns: ns,
            parentName: parentNamespace,
            inherits: inherits,
            isAbstract: isAbstract,
            accessibility: accessibility,
            sourceFilePath: sourceFilePath,
            properties: properties,
            nestedEntities: nestedEntities.Count > 0 ? nestedEntities : null,
            documentation: documentation,
            classOnly: classOnly,
            isStrict: isStrict,
            useParentValidator: useParentValidator,
            noCreateMethod: noCreateMethod,
            isRecordStruct: isRecordStruct)
        {
            LicenseHeader = licenseHeader
        };

        return genParams;
    }

    private static void GenerateCodeRecursive(DtoEntityGenerator.GeneratorParams genParams, List<(string fileName, string source)> generated, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var generatedCode = DtoEntityGenerator.Generate(genParams, null);
        if (!string.IsNullOrEmpty(generatedCode))
        {
            var fileName = $"{genParams.Namespace}.{genParams.Name}.g.cs";
            generated.Add((fileName, generatedCode!));
        }

        if (genParams.NestedEntities != null)
        {
            foreach (var nested in genParams.NestedEntities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GenerateCodeRecursive(nested, generated, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Validates property configuration based on the truth table for required/nullable/defaultValue combinations.
    /// Returns a tuple of (severity, message) if the configuration is invalid, or null if valid.
    /// Severity: true = Error, false = Warning
    /// </summary>
    private static (bool isError, string message)? ValidatePropertyConfiguration(string propertyName, string propertyType, bool isRequired, bool isNullable, bool hasDefault, bool isSettable, bool isStrict)
    {
        // For strict DTOs, settable must be false
        if (isStrict && isSettable)
        {
            return (true, $"Strict DTOs cannot have settable properties. Property '{propertyName}' has settable=true in a strict DTO.");
        }

        // Case #1 (Hard Invalid): required=false, nullable=false, no defaultValue
        // This creates a non-nullable type that's not required and has no default, which can stay null at runtime
        // However, this is only problematic for reference types. Value types have implicit defaults and cannot be null.
        if (!isRequired && !isNullable && !hasDefault)
        {
            // Only flag this as an error for reference types
            if (IsReferenceType(propertyType))
            {
                return (true, $"Invalid configuration for property '{propertyName}': Non-nullable reference type properties that are not required must have a default value. " +
                       $"Either set required=true, nullable=true, or provide a defaultValue.");
            }
            // For value types, this is safe - they have implicit defaults
        }

        // Case #8 (Hard Invalid): required=true, nullable=true, defaultValue present
        // This combination has confusing semantics: "required + nullable + default"
        if (isRequired && isNullable && hasDefault)
        {
            return (true, $"Invalid configuration for property '{propertyName}': Properties cannot be both required and nullable with a default value. " +
                   $"This combination has unclear semantics. Remove one of: required, nullable, or defaultValue.");
        }

        // Case #6 (Discouraged): required=true, nullable=false, defaultValue present
        // Default makes it effectively "non-nullable with default", but required suggests "must be provided"
        if (isRequired && !isNullable && hasDefault)
        {
            return (false, $"Discouraged configuration for property '{propertyName}': Required non-nullable properties should not have a default value. " +
                   $"If the property is required, the caller should provide it. Either remove 'required' or remove 'defaultValue'.");
        }

        // Case #7 (Discouraged): required=true, nullable=true, no defaultValue
        // Type says nullable but validation says non-nullable - logically inconsistent
        if (isRequired && isNullable && !hasDefault)
        {
            return (false, $"Inconsistent configuration for property '{propertyName}': Required properties should not be nullable. " +
                   $"The type allows null but validation requires non-null. Either remove 'required' or remove 'nullable'.");
        }

        // All other cases are valid
        return null;
    }

    /// <summary>
    /// Determines if a C# type string represents a reference type.
    /// This is a heuristic based on common C# type patterns.
    /// </summary>
    private static bool IsReferenceType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return true; // Conservative default
        }

        // Extract base type name, handling generics, arrays, and nullable markers
        var baseType = ExtractBaseTypeName(typeName);

        // Check if it's a known value type
        if (KnownValueTypes.Contains(baseType))
        {
            return false; // It's a value type
        }

        // Everything else is assumed to be a reference type (classes, strings, custom types, etc.)
        // Note: This includes "string" which is a reference type
        return true;
    }

    /// <summary>
    /// Extracts the base type name from a potentially complex type string.
    /// Handles nullable markers (?), array brackets ([]), and generic type parameters.
    /// </summary>
    private static string ExtractBaseTypeName(string typeName)
    {
        // Remove leading/trailing whitespace
        var cleaned = typeName.Trim();
        
        // Remove nullable suffix (?)
        if (cleaned.EndsWith("?"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 1);
        }
        
        // Remove array brackets ([], [,], etc.)
        var bracketIndex = cleaned.IndexOf('[');
        if (bracketIndex >= 0)
        {
            cleaned = cleaned.Substring(0, bracketIndex);
        }
        
        // Remove generic type parameters (everything after <)
        var genericIndex = cleaned.IndexOf('<');
        if (genericIndex >= 0)
        {
            cleaned = cleaned.Substring(0, genericIndex);
        }
        
        return cleaned.Trim();
    }

    // Static set of known value types for efficient lookup
    private static readonly HashSet<string> KnownValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float",
        "int", "uint", "long", "ulong", "short", "ushort",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
        "System.Boolean", "System.Byte", "System.SByte", "System.Char",
        "System.Decimal", "System.Double", "System.Single",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64",
        "System.Int16", "System.UInt16",
        "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid"
    };
}
