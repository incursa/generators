// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;

namespace Bravellian.Generators.SqlGen.Pipeline;

/// <summary>
/// Simplified version of SqlEntitySourceGenerator for CLI usage.
/// Contains the core generation logic without the Roslyn source generator infrastructure.
/// </summary>
public class SqlEntityCliGenerator
{
    /// <summary>
    /// Generates C# entity source files from SQL schema files and optional JSON configuration.
    /// This is the primary method that follows the "convention over configuration" principle.
    /// </summary>
    /// <param name="sqlFiles">Dictionary of SQL file paths and their content.</param>
    /// <param name="sqlConfigFiles">Dictionary of JSON SQL configuration file paths and their content (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of generated file names and their source code.</returns>
    public IEnumerable<(string fileName, string source)> GenerateFromFiles(
        IDictionary<string, string> sqlFiles, 
        IDictionary<string, string> sqlConfigFiles,
        CancellationToken cancellationToken = default)
    {
        var logger = new CliSqlGenLogger();
        var results = new List<(string fileName, string source)>();
        
        try
        {
            // Load SQL configuration (generator.config.json) if provided
            SqlConfiguration? sqlConfig = null;
            string? originalConfigJson = null;
            if (sqlConfigFiles.Any())
            {
                foreach (var configFile in sqlConfigFiles)
                {
                    try
                    {
                        originalConfigJson = configFile.Value;
                        sqlConfig = SqlConfiguration.FromJson(originalConfigJson);
                        logger.LogMessage($"Loaded SQL configuration from {configFile.Key}");
                        break; // Use the first valid config file found
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage($"Failed to parse SQL configuration from {configFile.Key}: {ex.Message}");
                    }
                }
            }
            
            if (sqlConfig == null)
            {
                logger.LogMessage("No valid SQL configuration found. Using default conventions.");
                sqlConfig = new SqlConfiguration(); // Use empty configuration for pure convention-based generation
            }
            
            // Set up the usage tracker
            var usageTracker = new UsedConfigurationTracker(originalConfigJson);

            // Set up the pipeline components
            var schemaIngestor = new SqlSchemaIngestor(logger);
            var schemaRefiner = new SchemaRefiner(logger, sqlConfig, usageTracker);
            var cSharpModelTransformer = new CSharpModelTransformer(logger, sqlConfig, usageTracker);
            var cSharpCodeGenerator = new CSharpCodeGenerator(sqlConfig, logger);
            
            // Create the orchestrator and run the pipeline
            var orchestrator = new SqlGenOrchestrator(
                schemaIngestor,
                schemaRefiner,
                cSharpModelTransformer,
                cSharpCodeGenerator,
                sqlConfig,
                logger);
            
            // Convert SQL files dictionary to array of SQL content
            var sqlContents = sqlFiles.Values.ToArray();
            
            // Run the generation pipeline
            var generatedCode = orchestrator.Generate(sqlContents);
            
            // Convert results to the expected format
            foreach (var kvp in generatedCode)
            {
                results.Add((kvp.Key, kvp.Value));
            }

            // Add the used configuration file to the results
            var usedConfigJson = usageTracker.GetUsedConfigurationAsJson();
            if (!string.IsNullOrEmpty(usedConfigJson))
            {
                results.Add(("generator.config.used.json", usedConfigJson));
                logger.LogMessage("Generated used configuration file.");
            }
            
            logger.LogMessage($"Successfully generated {results.Count} files.");
        }
        catch (Exception ex)
        {
            logger.LogMessage($"Code generation failed: {ex.Message}");
            var errorFileName = "GenerationError.txt";
            var errorSource = $"// Code generation failed\n// Error: {ex.Message}\n// Stack trace:\n// {ex.StackTrace}";
            results.Add((errorFileName, errorSource));
        }
        
        return results;
    }

    /// <summary>
    /// Legacy method for backward compatibility - supports XML type mapping and property files.
    /// </summary>
    /// <param name="sqlFiles">Dictionary of SQL file paths and their content.</param>
    /// <param name="sqlConfigFiles">Dictionary of JSON SQL configuration file paths and their content.</param>
    /// <param name="typeMappingFiles">Dictionary of XML type mapping file paths and their content (legacy).</param>
    /// <param name="propertyFiles">Dictionary of XML property configuration file paths and their content (legacy).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of generated file names and their source code.</returns>
    public IEnumerable<(string fileName, string source)> GenerateFromFiles(
        IDictionary<string, string> sqlFiles, 
        IDictionary<string, string> sqlConfigFiles,
        IDictionary<string, string> typeMappingFiles,
        IDictionary<string, string> propertyFiles, 
        CancellationToken cancellationToken = default)
    {
        // For now, just delegate to the primary method, ignoring legacy XML files
        // TODO: Add legacy XML support if needed
        return GenerateFromFiles(sqlFiles, sqlConfigFiles, cancellationToken);
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public IEnumerable<(string fileName, string source)> GenerateFromFiles(
        IDictionary<string, string> sqlFiles, 
        IDictionary<string, string> typeMappingFiles, 
        string baseNamespace = "Generated.Entities",
        CancellationToken cancellationToken = default)
    {
        // Delegate to the primary method with empty config files
        return GenerateFromFiles(
            sqlFiles, 
            new Dictionary<string, string>(), // No JSON config files
            cancellationToken);
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public IEnumerable<(string fileName, string source)> GenerateFromFiles(
        IDictionary<string, string> sqlFiles, 
        IDictionary<string, string> typeMappingFiles,
        IDictionary<string, string> propertyFiles, 
        CancellationToken cancellationToken = default)
    {
        // Delegate to the primary method with empty config files
        return GenerateFromFiles(
            sqlFiles, 
            new Dictionary<string, string>(), // No JSON config files
            cancellationToken);
    }

    /// <summary>
    /// Simplified public method for CLI usage when working with a single SQL file.
    /// </summary>
    /// <param name="filePath">The SQL file path.</param>
    /// <param name="fileContent">The SQL file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of generated file names and their source code.</returns>
    public IEnumerable<(string fileName, string source)>? GenerateFromFiles(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        var sqlFiles = new Dictionary<string, string> { { filePath, fileContent } };
        return GenerateFromFiles(
            sqlFiles, 
            new Dictionary<string, string>(), // No JSON config files
            cancellationToken);
    }
    
    /// <summary>
    /// Generates C# entity source files from the collected SQL schema files and type mapping configuration.
    /// </summary>
    /// <param name="files">The SQL schema files and configuration files with their paths, content, file types, and optional namespace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of generated file names and their source code.</returns>
    public IEnumerable<(string fileName, string source)> Generate(
        ImmutableArray<(string Path, string Content, string FileType, string? Namespace)> files, 
        CancellationToken cancellationToken)
    {
        if (files.IsEmpty)
        {
            return Enumerable.Empty<(string, string)>();
        }

        // Separate files by type
        var sqlFiles = files
            .Where(f => f.FileType == "SqlSchema")
            .ToDictionary(f => f.Path, f => f.Content);
            
        var sqlConfigFiles = files
            .Where(f => f.FileType == "SqlConfig")
            .ToDictionary(f => f.Path, f => f.Content);
        
        return GenerateFromFiles(sqlFiles, sqlConfigFiles, cancellationToken);
    }
}

/// <summary>
/// Simple console logger implementation for CLI usage.
/// </summary>
internal class CliSqlGenLogger : IBvLogger
{
    public void LogMessage(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public void LogError(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }

    public void LogError(string message, Exception ex)
    {
        Console.WriteLine($$"""
        [ERROR] {{message}}
        [EXCEPTION] {{ex.Message}}
        """);
            
    }

    public void LogErrorFromException(Exception ex)
    {
        Console.WriteLine($$"""
        [EXCEPTION] {{ex.Message}}
        """);
    }
}

