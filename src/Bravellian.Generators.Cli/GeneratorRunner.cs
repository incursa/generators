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

namespace Bravellian.Generators.Cli;

/// <summary>
/// Main runner that orchestrates all generators
/// </summary>
public class GeneratorRunner
{
    private readonly bool _verbose;
    private readonly List<CliGenerator> _generators;

    public GeneratorRunner(bool verbose = false)
    {
        _verbose = verbose;
        _generators = new List<CliGenerator>
        {
            new StringBackedEnumCliGenerator(),
            new StringBackedTypeCliGenerator(),
            new DtoEntityCliGenerator(),
            new FastIdBackedTypeCliGenerator(),
            new GuidBackedTypeCliGenerator(),
            new GenericBackedTypeCliGenerator(),
            new MultiValueBackedTypeCliGenerator(),
            new NumberBackedEnumTypeCliGenerator(),
            new CapabilityCliGenerator(),
            new SqlEntityCliGenerator()
        };
    }

    public async Task RunAsync(string[] inputPaths, DirectoryInfo outputDir, bool dryRun)
    {
        try
        {
            LogInfo($"Starting Bravellian code generation...");
            LogInfo($"Input paths: {string.Join(", ", inputPaths)}");
            LogInfo($"Output directory: {outputDir.FullName}");
            LogInfo($"Dry run: {dryRun}");
            LogInfo($"Generators: {_generators.Count}");

            // Validate all input paths exist
            var validatedPaths = new List<string>();
            foreach (var inputPath in inputPaths)
            {
                if (File.Exists(inputPath))
                {
                    LogInfo($"Input file: {inputPath}");
                    validatedPaths.Add(inputPath);
                }
                else if (Directory.Exists(inputPath))
                {
                    LogInfo($"Input directory: {inputPath}");
                    validatedPaths.Add(inputPath);
                }
                else
                {
                    LogError($"Input path does not exist: {inputPath}");
                    return;
                }
            }

            if (!validatedPaths.Any())
            {
                LogError("No valid input paths provided");
                return;
            }

            // Prepare output directory
            if (!dryRun)
            {
                if (outputDir.Exists)
                {
                    LogInfo("Clearing existing output directory...");
                    outputDir.Delete(true);
                }
                outputDir.Create();
                LogInfo("Output directory prepared.");
            }

            var totalFilesGenerated = 0;
            var totalErrors = 0;

            // Run each generator
            foreach (var generator in _generators)
            {
                LogInfo($"Running generator: {generator.Name}");
                
                try
                {
                    var results = await generator.GenerateAsync(validatedPaths);
                    var generatedFiles = results.ToList();
                    
                    LogInfo($"  {generator.Name}: Generated {generatedFiles.Count} files");

                    foreach (var (fileName, source) in generatedFiles)
                    {
                        if (dryRun)
                        {
                            LogInfo($"  [DRY RUN] Would generate: {fileName}");
                        }
                        else
                        {
                            var outputPath = Path.Combine(outputDir.FullName, fileName);
                            var fileDir = Path.GetDirectoryName(outputPath);
                            
                            if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                            {
                                Directory.CreateDirectory(fileDir);
                            }
                            
                            await File.WriteAllTextAsync(outputPath, source);
                            LogVerbose($"  Generated: {fileName}");
                        }
                        
                        totalFilesGenerated++;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"  Error in {generator.Name}: {ex.Message}");
                    if (_verbose)
                    {
                        LogError($"  Stack trace: {ex.StackTrace}");
                    }
                    totalErrors++;
                }
            }

            LogInfo($"Generation complete. Total files: {totalFilesGenerated}, Errors: {totalErrors}");
            
            if (totalErrors > 0)
            {
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            LogError($"Fatal error: {ex.Message}");
            if (_verbose)
            {
                LogError($"Stack trace: {ex.StackTrace}");
            }
            Environment.ExitCode = 1;
        }
    }

    private void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    private void LogError(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }

    private void LogVerbose(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[VERBOSE] {message}");
        }
    }
}
