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
public class GenericBackedTypeSourceGenerator
{
    // 2. Specify which files to watch
    protected Regex FileExtensionRegex { get; } = new Regex(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml)$");

    // 3. Implement the generation logic
    protected IEnumerable<(string fileName, string source)>? Generate(string filePath, string fileContent, CancellationToken cancellationToken)
    {
        // Your existing logic fits right in here.
        var xdoc = XDocument.Parse(fileContent);
        if (xdoc.Root == null) return null;

        var elements = xdoc.Root.Elements("GenericBacked");
        if (!elements.Any()) return null;

        List<(string fileName, string source)> generated = new();
        foreach (var element in elements)
        {
            var genParams = GenericBackedTypeGenerator.GetParams(element, null);
            if (genParams == null) continue;

            var generatedCode = GenericBackedTypeGenerator.Generate(genParams, null);
            if (string.IsNullOrEmpty(generatedCode)) continue;

            var fileName = $"{genParams.Value.Namespace}.{genParams.Value.Name}.g.cs";

            generated.Add((fileName, generatedCode!));
        }

        return generated;
    }

    /// <summary>
    /// Public wrapper for CLI usage
    /// </summary>
    public IEnumerable<(string fileName, string source)>? GenerateFromFiles(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        return Generate(filePath, fileContent, cancellationToken);
    }
}
