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

using System.Text.RegularExpressions;
using Bravellian.Generators;

namespace Bravellian.Generators.Cli;

/// <summary>
/// Wrapper for source generators to run as CLI generators
/// </summary>
public abstract class CliGenerator
{
    public abstract string Name { get; }
    public abstract Regex FileExtensionRegex { get; }
    
    public abstract Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default);
    
    protected static IEnumerable<string> FindFiles(IEnumerable<string> paths, Regex pattern)
    {
        var allFiles = new List<string>();
        
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                // If it's a file, check if it matches the pattern
                if (pattern.IsMatch(path))
                {
                    allFiles.Add(path);
                }
            }
            else if (Directory.Exists(path))
            {
                // If it's a directory, recursively find files
                var directoryFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => pattern.IsMatch(f));
                allFiles.AddRange(directoryFiles);
            }
        }
        
        return allFiles;
    }
}

/// <summary>
/// CLI wrapper for StringBackedEnumTypeSourceGenerator
/// </summary>
public class StringBackedEnumCliGenerator : CliGenerator
{
    public override string Name => "StringBackedEnum";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.enum\.json|.*\.string_enum\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new StringBackedEnumTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for DtoEntitySourceGenerator
/// </summary>
public class DtoEntityCliGenerator : CliGenerator
{
    public override string Name => "DtoEntity";
    public override Regex FileExtensionRegex => new(@"(?:.*\.dto\.xml|.*\.entities\.xml|.*\.viewmodels\.xml|_generate\.xml|.*\.dto\.json|.*\.entity\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new DtoEntitySourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for FastIdBackedTypeSourceGenerator
/// </summary>
public class FastIdBackedTypeCliGenerator : CliGenerator
{
    public override string Name => "FastIdBackedType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.fastid\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new FastIdBackedTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for GuidBackedTypeSourceGenerator
/// </summary>
public class GuidBackedTypeCliGenerator : CliGenerator
{
    public override string Name => "GuidBackedType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.guid\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new GuidBackedTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for GenericBackedTypeSourceGenerator
/// </summary>
public class GenericBackedTypeCliGenerator : CliGenerator
{
    public override string Name => "GenericBackedType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new GenericBackedTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for MultiValueBackedTypeSourceGenerator
/// </summary>
public class MultiValueBackedTypeCliGenerator : CliGenerator
{
    public override string Name => "MultiValueBackedType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.multi\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new MultiValueBackedTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for NumberBackedEnumTypeSourceGenerator
/// </summary>
public class NumberBackedEnumTypeCliGenerator : CliGenerator
{
    public override string Name => "NumberBackedEnumType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.number_enum\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new NumberBackedEnumTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for StringBackedTypeSourceGenerator
/// </summary>
public class StringBackedTypeCliGenerator : CliGenerator
{
    public override string Name => "StringBackedType";
    public override Regex FileExtensionRegex => new(@"(?:.*\.types\.xml|.*\.sbt\.xml|_generate\.xml|.*\.string\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new StringBackedTypeSourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for CapabilitySourceGenerator
/// </summary>
public class CapabilityCliGenerator : CliGenerator
{
    public override string Name => "Capability";
    public override Regex FileExtensionRegex => new(@"(?:.*\.capabilities\.xml|.*\.capabilities\.json|.*\.capabilities-only\.json|.*\.adapter-profile\.json|.*\.erp-capabilities\.xml|.*\.erp-capabilities\.json|.*\.erp-capabilities-only\.json|.*\.erp-adapter-profile\.json|_generate\.xml)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new CapabilitySourceGenerator();
        var results = new List<(string fileName, string source)>();
        
        var files = FindFiles(inputPaths, FileExtensionRegex);
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var generated = generator.GenerateFromFiles(filePath, fileContent, cancellationToken);
            
            if (generated != null)
            {
                results.AddRange(generated);
            }
        }
        
        return results;
    }
}

/// <summary>
/// CLI wrapper for SqlEntitySourceGenerator
/// </summary>
public class SqlEntityCliGenerator : CliGenerator
{
    public override string Name => "SqlEntity";
    public override Regex FileExtensionRegex => new(@"(?i)\.(sql|generator\.config\.json)$");

    public override async Task<IEnumerable<(string fileName, string source)>> GenerateAsync(
        IEnumerable<string> inputPaths, 
        CancellationToken cancellationToken = default)
    {
        var generator = new Bravellian.Generators.SqlGen.Pipeline.SqlEntityCliGenerator();
        var results = new List<(string fileName, string source)>();
        
        // Find all relevant files first
        var allFiles = FindFiles(inputPaths, FileExtensionRegex).ToList();
        
        var sqlFiles = new Dictionary<string, string>();
        var sqlConfigFiles = new Dictionary<string, string>();
        
        foreach (var filePath in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            if (filePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                sqlFiles[filePath] = fileContent;
            }
            else if (filePath.EndsWith("generator.config.json", StringComparison.OrdinalIgnoreCase))
            {
                sqlConfigFiles[filePath] = fileContent;
            }
        }
        
        if (sqlFiles.Any())
        {
            // Use the simplified method that only takes SQL files and JSON config files
            var generated = generator.GenerateFromFiles(sqlFiles, sqlConfigFiles, cancellationToken);
            results.AddRange(generated);
        }
        
        return results;
    }
}
