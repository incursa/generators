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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;


// 1. Inherit from the base class for single files
// [Generator]
public class StringBackedEnumTypeSourceGenerator
{
    // 2. Specify which files to watch
    protected Regex FileExtensionRegex { get; } = new Regex(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.enum\.json|.*\.string_enum\.json)$");

    // 3. Implement the generation logic
    protected IEnumerable<(string fileName, string source)>? Generate(string filePath, string fileContent, CancellationToken cancellationToken)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        
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
        // Your existing XML logic fits right in here.
        var xdoc = XDocument.Parse(fileContent);
        if (xdoc.Root == null) return null;

        var elements = xdoc.Root.Elements("StringEnum");
        if (!elements.Any()) return null;

        List<(string fileName, string source)> generated = new();
        foreach (var element in elements)
        {
            var genParams = StringBackedEnumTypeGenerator.GetParams(element, null);
            if (genParams == null) continue;

            var generatedCode = StringBackedEnumTypeGenerator.Generate(genParams, null);
            if (string.IsNullOrEmpty(generatedCode)) continue;

            var fileName = $"{genParams.Value.Namespace}.{genParams.Value.Name}.g.cs";

            generated.Add((fileName, generatedCode!));
        }

        return generated;
    }

    private IEnumerable<(string fileName, string source)>? GenerateFromJson(string fileContent)
    {
        try
        {
            using var document = JsonDocument.Parse(fileContent);
            var root = document.RootElement;

            if (!root.TryGetProperty("name", out var nameElement) ||
                !root.TryGetProperty("namespace", out var namespaceElement) ||
                !root.TryGetProperty("values", out var valuesElement))
            {
                return null;
            }

            var name = nameElement.GetString();
            var namespaceName = namespaceElement.GetString();
            
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(namespaceName))
            {
                return null;
            }

            var enumValues = new List<(string Value, string Name, string? DisplayName, string? Documentation)>();

            foreach (var valueProperty in valuesElement.EnumerateObject())
            {
                var valueName = valueProperty.Name;
                var valueObj = valueProperty.Value;

                if (!valueObj.TryGetProperty("value", out var valueElement))
                {
                    continue;
                }

                var value = valueElement.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                var displayName = valueObj.TryGetProperty("display", out var displayElement) 
                    ? displayElement.GetString() 
                    : valueName;

                var documentation = valueObj.TryGetProperty("documentation", out var docElement) 
                    ? docElement.GetString() 
                    : null;

                enumValues.Add((value!, valueName, displayName, documentation));
            }

            if (!enumValues.Any())
            {
                return null;
            }

            // Parse additional properties
            var additionalProperties = new List<(string Type, string Name)>();
            if (root.TryGetProperty("properties", out var propertiesElement))
            {
                foreach (var property in propertiesElement.EnumerateObject())
                {
                    var propertyName = property.Name;
                    var propertyObj = property.Value;

                    if (propertyObj.TryGetProperty("type", out var typeElement))
                    {
                        var typeName = typeElement.GetString();
                        if (!string.IsNullOrEmpty(typeName))
                        {
                            additionalProperties.Add((typeName!, propertyName));
                        }
                    }
                }
            }

            var genParams = new StringBackedEnumTypeGenerator.GeneratorParams(
                name!, 
                namespaceName!, 
                true, 
                enumValues, 
                additionalProperties
            );

            var generatedCode = StringBackedEnumTypeGenerator.Generate(genParams, null);
            if (string.IsNullOrEmpty(generatedCode))
            {
                return null;
            }

            var fileName = $"{namespaceName!}.{name!}.g.cs";
            return new[] { (fileName, generatedCode!) };
        }
        catch
        {
            return null;
        }
    }
}
