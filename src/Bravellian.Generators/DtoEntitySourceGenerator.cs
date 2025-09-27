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

namespace Bravellian.Generators;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;


// 1. Inherit from the base class for single files
// [Generator]
public class DtoEntitySourceGenerator
{
    // 2. Specify which files to watch for DTO definitions
    protected Regex FileExtensionRegex { get; } = new Regex(@"(?:.*\.dto\.xml|.*\.entities\.xml|.*\.viewmodels\.xml|_generate\.xml|.*\.dto\.json|.*\.entity\.json)$");

    // 3. Implement the generation logic
    protected IEnumerable<(string fileName, string source)>? Generate(string filePath, string fileContent, CancellationToken cancellationToken)
    {
        var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        if (fileExtension == ".json")
        {
            return GenerateFromJson(fileContent);
        }
        else
        {
            return GenerateFromXml(fileContent);
        }
    }

    /// <summary>
    /// Public wrapper for CLI usage
    /// </summary>
    public IEnumerable<(string fileName, string source)>? GenerateFromFiles(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        return Generate(filePath, fileContent, cancellationToken);
    }

    private IEnumerable<(string fileName, string source)>? GenerateFromXml(string fileContent)
    {
        var xdoc = XDocument.Parse(fileContent);
        if (xdoc.Root == null) return null;

        // Look for DtoEntity, Entity, ViewModel, or Dto elements
        var elements = xdoc.Root.Elements()
            .Where(e => e.Name.LocalName == "DtoEntity" || 
                       e.Name.LocalName == "Entity" || 
                       e.Name.LocalName == "ViewModel" || 
                       e.Name.LocalName == "Dto");
        
        if (!elements.Any()) return null;

        List<(string fileName, string source)> generated = new();
        
        foreach (var element in elements)
        {
            var genParams = GetParamsFromXml(element, null);
            if (genParams == null) continue;

            var generatedCode = DtoEntityGenerator.Generate(genParams, null);
            if (string.IsNullOrEmpty(generatedCode)) continue;

            // Generate filename based on namespace and name
            var fileName = $"{genParams.Namespace}.{genParams.Name}.g.cs";

            generated.Add((fileName, generatedCode!));
        }

        return generated;
    }

    private IEnumerable<(string fileName, string source)>? GenerateFromJson(string fileContent)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(fileContent);
            var root = document.RootElement;

            var genParams = ParseGeneratorParamsFromJson(root);
            if (genParams == null)
            {
                return null;
            }

            var generated = new List<(string fileName, string source)>();
            GenerateCodeRecursive(genParams, generated);
            return generated;
        }
        catch
        {
            return null;
        }
    }

    private static DtoEntityGenerator.GeneratorParams? ParseGeneratorParamsFromJson(System.Text.Json.JsonElement root)
    {
        // Required fields
        if (!root.TryGetProperty("name", out var nameElement) ||
            !root.TryGetProperty("namespace", out var namespaceElement))
        {
            return null;
        }
        var name = nameElement.GetString();
        var ns = namespaceElement.GetString();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ns))
        {
            return null;
        }

        // Optional fields
        string? parentName = root.TryGetProperty("parent", out var parentElement) ? parentElement.GetString() : null;
        string? inherits = root.TryGetProperty("inherits", out var inheritsElement) ? inheritsElement.GetString() : null;
        bool isAbstract = root.TryGetProperty("abstract", out var absElement) && absElement.ValueKind == System.Text.Json.JsonValueKind.True;
        string? accessibility = root.TryGetProperty("accessibility", out var accElement) ? accElement.GetString() : null;
        string? documentation = root.TryGetProperty("documentation", out var docElement) ? docElement.GetString() : null;
        bool classOnly = root.TryGetProperty("classOnly", out var classOnlyElement) && classOnlyElement.ValueKind == System.Text.Json.JsonValueKind.True;

        // Properties
        var properties = new List<DtoEntityGenerator.PropertyDescriptor>();
        if (root.TryGetProperty("properties", out var propsElement) && propsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var prop in propsElement.EnumerateArray())
            {
                var propName = prop.TryGetProperty("name", out var n) ? n.GetString() : null;
                var propType = prop.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propType)) continue;

                var isRequired = prop.TryGetProperty("required", out var req) && req.ValueKind == System.Text.Json.JsonValueKind.True;
                var isNullable = prop.TryGetProperty("nullable", out var nul) && nul.ValueKind == System.Text.Json.JsonValueKind.True;
                var noDefault = prop.TryGetProperty("noDefault", out var noDef) && noDef.ValueKind == System.Text.Json.JsonValueKind.True;
                var isSettable = !prop.TryGetProperty("settable", out var set) || (set.ValueKind == System.Text.Json.JsonValueKind.True);
                var max = prop.TryGetProperty("max", out var maxEl) ? maxEl.GetString() : null;
                var min = prop.TryGetProperty("min", out var minEl) ? minEl.GetString() : null;
                var regex = prop.TryGetProperty("regex", out var regexEl) ? regexEl.GetString() : null;
                var jsonProperty = prop.TryGetProperty("jsonProperty", out var jsonPropEl) ? jsonPropEl.GetString() : null;
                var expression = prop.TryGetProperty("expression", out var exprEl) ? exprEl.GetString() : null;
                var propDocumentation = prop.TryGetProperty("documentation", out var docEl) ? docEl.GetString() : null;

                properties.Add(new DtoEntityGenerator.PropertyDescriptor(
                    name: propName!,
                    type: propType!,
                    isRequired: isRequired,
                    isNullable: isNullable,
                    max: max,
                    min: min,
                    regex: regex,
                    jsonProperty: jsonProperty,
                    noDefault: noDefault,
                    isSettable: isSettable,
                    expression: expression,
                    documentation: propDocumentation
                ));
            }
        }

        // Nested entities
        List<DtoEntityGenerator.GeneratorParams>? nestedEntities = null;
        if (root.TryGetProperty("nestedEntities", out var nestedElement) && nestedElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            nestedEntities = new List<DtoEntityGenerator.GeneratorParams>();
            foreach (var nested in nestedElement.EnumerateArray())
            {
                var nestedParams = ParseGeneratorParamsFromJson(nested);
                if (nestedParams != null)
                {
                    // Determine isAbstract for nested entity
                    bool nestedIsAbstract = false;
                    if (nested.TryGetProperty("abstract", out var nestedAbsElement))
                        nestedIsAbstract = nestedAbsElement.ValueKind == System.Text.Json.JsonValueKind.True;

                    // For nested entities, set parentName and classOnly
                    nestedParams = new DtoEntityGenerator.GeneratorParams(
                        name: nestedParams.Name,
                        ns: ns!, // Use parent namespace
                        parentName: name!, // Set parent name
                        inherits: nestedParams.Inherits?.TrimStart(' ', ':'),
                        isAbstract: nestedIsAbstract,
                        accessibility: nestedParams.Accessibility,
                        properties: nestedParams.Properties,
                        nestedEntities: nestedParams.NestedEntities,
                        documentation: nestedParams.Documentation,
                        classOnly: true // Force classOnly for nested entities
                    );
                    nestedEntities.Add(nestedParams);
                }
            }
        }

        return new DtoEntityGenerator.GeneratorParams(
            name: name!,
            ns: ns!,
            parentName: parentName,
            inherits: inherits,
            isAbstract: isAbstract,
            accessibility: accessibility,
            properties: properties,
            nestedEntities: nestedEntities,
            documentation: documentation,
            classOnly: classOnly
        );
    }

    private static void GenerateCodeRecursive(DtoEntityGenerator.GeneratorParams genParams, List<(string fileName, string source)> generated)
    {
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
                GenerateCodeRecursive(nested, generated);
            }
        }
    }

    /// <summary>
    /// Parse XML element to create GeneratorParams for DTO generation
    /// </summary>
    public static DtoEntityGenerator.GeneratorParams? GetParamsFromXml(XElement xml, IBvLogger? logger)
    {
        var attributes = xml.GetAttributeDict();
        
        // Required attributes
        if (!attributes.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
        {
            logger?.LogError("DTO entity must have a 'name' attribute");
            return null;
        }

        if (!attributes.TryGetValue("namespace", out var ns) || string.IsNullOrEmpty(ns))
        {
            logger?.LogError($"DTO entity '{name}' must have a 'namespace' attribute");
            return null;
        }

        // Optional attributes
        attributes.TryGetValue("parent", out var parentName);
        attributes.TryGetValue("inherits", out var inherits);
        var isAbstract = attributes.TryGetValue("abstract", out var abstractStr) && bool.TryParse(abstractStr, out var abs) && abs;
        attributes.TryGetValue("accessibility", out var accessibility);
        var classOnly = attributes.TryGetValue("classOnly", out var classOnlyStr) && bool.TryParse(classOnlyStr, out var co) && co;
        attributes.TryGetValue("documentation", out var documentation);

        // Parse properties
        var properties = ParseProperties(xml.Elements("Property"), logger);
        
        // Parse nested entities
        var nestedEntities = ParseNestedEntities(xml.Elements("NestedEntity"), ns, name, logger);

        return new DtoEntityGenerator.GeneratorParams(
            name: name,
            ns: ns,
            parentName: parentName,
            inherits: inherits,
            isAbstract: isAbstract,
            accessibility: accessibility,
            properties: properties,
            nestedEntities: nestedEntities,
            documentation: documentation,
            classOnly: classOnly);
    }

    private static List<DtoEntityGenerator.PropertyDescriptor> ParseProperties(IEnumerable<XElement> propertyElements, IBvLogger? logger)
    {
        var properties = new List<DtoEntityGenerator.PropertyDescriptor>();

        foreach (var prop in propertyElements)
        {
            var attributes = prop.GetAttributeDict();
            
            if (!attributes.TryGetValue("name", out var propName) || string.IsNullOrEmpty(propName))
            {
                logger?.LogError("Property must have a 'name' attribute");
                continue;
            }

            if (!attributes.TryGetValue("type", out var propType) || string.IsNullOrEmpty(propType))
            {
                logger?.LogError($"Property '{propName}' must have a 'type' attribute");
                continue;
            }

            var isRequired = attributes.TryGetValue("required", out var reqStr) && bool.TryParse(reqStr, out var req) && req;
            var isNullable = attributes.TryGetValue("nullable", out var nullStr) && bool.TryParse(nullStr, out var nul) && nul;
            var noDefault = attributes.TryGetValue("noDefault", out var noDefStr) && bool.TryParse(noDefStr, out var noDef) && noDef;
            var isSettable = !attributes.ContainsKey("settable") || (attributes.TryGetValue("settable", out var setStr) && bool.TryParse(setStr, out var set) && set);

            attributes.TryGetValue("max", out var max);
            attributes.TryGetValue("min", out var min);
            attributes.TryGetValue("regex", out var regex);
            attributes.TryGetValue("jsonProperty", out var jsonProperty);
            attributes.TryGetValue("expression", out var expression);
            attributes.TryGetValue("documentation", out var propDocumentation);

            properties.Add(new DtoEntityGenerator.PropertyDescriptor(
                name: propName,
                type: propType,
                isRequired: isRequired,
                isNullable: isNullable,
                max: max,
                min: min,
                regex: regex,
                jsonProperty: jsonProperty,
                noDefault: noDefault,
                isSettable: isSettable,
                expression: expression,
                documentation: propDocumentation));
        }

        return properties;
    }

    private static List<DtoEntityGenerator.GeneratorParams> ParseNestedEntities(IEnumerable<XElement> nestedElements, string parentNamespace, string parentName, IBvLogger? logger)
    {
        var nestedEntities = new List<DtoEntityGenerator.GeneratorParams>();

        foreach (var nested in nestedElements)
        {
            var nestedParams = GetParamsFromXml(nested, logger);
            if (nestedParams != null)
            {
                // For nested entities, we want to set the parent name and make them class-only
                var updatedParams = new DtoEntityGenerator.GeneratorParams(
                    name: nestedParams.Name,
                    ns: parentNamespace, // Use parent namespace
                    parentName: parentName, // Set parent name
                    inherits: nestedParams.Inherits?.TrimStart(' ', ':'), // Remove leading colon if present
                    isAbstract: !string.IsNullOrEmpty(nestedParams.Abstract),
                    accessibility: nestedParams.Accessibility,
                    properties: nestedParams.Properties,
                    nestedEntities: nestedParams.NestedEntities,
                    documentation: nestedParams.Documentation,
                    classOnly: true); // Force class-only for nested entities

                nestedEntities.Add(updatedParams);
            }
        }

        return nestedEntities;
    }
}
