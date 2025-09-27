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
public class MultiValueBackedTypeSourceGenerator
{
    // 2. Specify which files to watch
    protected Regex FileExtensionRegex { get; } = new Regex(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.multi\.json)$");

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
        var xdoc = XDocument.Parse(fileContent);
        if (xdoc.Root == null) return null;

        var elements = xdoc.Root.Elements("MultiValueString");
        if (!elements.Any()) return null;

        List<(string fileName, string source)> generated = new();
        foreach (var element in elements)
        {
            var genParams = MultiValueBackedTypeGenerator.GetParams(element, null);
            if (genParams == null) continue;

            var generatedCode = MultiValueBackedTypeGenerator.Generate(genParams, null);
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
                !root.TryGetProperty("namespace", out var namespaceElement))
            {
                return null;
            }

            var name = nameElement.GetString();
            var namespaceName = namespaceElement.GetString();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(namespaceName))
            {
                return null;
            }

            string? separator = null;
            if (root.TryGetProperty("separator", out var separatorElement))
                separator = separatorElement.GetString();

            string? format = null;
            if (root.TryGetProperty("format", out var formatElement))
                format = formatElement.GetString();

            string? regex = null;
            if (root.TryGetProperty("regex", out var regexElement))
                regex = regexElement.GetString();

            // Add bookend character support
            string? bookend = null;
            if (root.TryGetProperty("bookend", out var bookendElement))
                bookend = bookendElement.GetString();

            string? nullIdentifier = null;
            if (root.TryGetProperty("nullIdentifier", out var nullIdentifierElement))
                nullIdentifier = nullIdentifierElement.GetString();

            var fields = new List<MultiValueBackedTypeGenerator.FieldInfo>();
            if (root.TryGetProperty("parts", out var partsElement) && partsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in partsElement.EnumerateArray())
                {
                    var fieldName = part.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var fieldType = part.TryGetProperty("type", out var t) ? t.GetString() : "string";
                    var serializedName = part.TryGetProperty("serializedName", out var sn) ? sn.GetString() : null;
                    var constantValue = part.TryGetProperty("constantValue", out var cv) ? cv.GetString() : null;
                    var constantTypeValue = part.TryGetProperty("constantTypeValue", out var ctv) ? ctv.GetString() : null;
                    var isNullable = part.TryGetProperty("isNullable", out var inull) && inull.GetBoolean();
                    var partNullIdentifier = part.TryGetProperty("nullIdentifier", out var pni) ? pni.GetString() : null;

                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        fields.Add(new MultiValueBackedTypeGenerator.FieldInfo(
                            fieldName!,
                            fieldType!,
                            serializedName,
                            constantValue,
                            constantTypeValue,
                            isNullable,
                            partNullIdentifier ?? nullIdentifier
                        ));
                    }
                }
            }

            var genParams = new MultiValueBackedTypeGenerator.GeneratorParams(
                name!,
                namespaceName!,
                true,
                separator ?? "|",
                format ?? string.Empty,
                regex ?? string.Empty,
                bookend ?? string.Empty,
                fields
            );

            var generatedCode = MultiValueBackedTypeGenerator.Generate(genParams, null);
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
