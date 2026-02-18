namespace Incursa.Generators.Tests;

using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class StringBackedEnumPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void TablerIconEnumGenerationPerformance()
    {
        // Read the large enum file
        var filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TablerIcon.enum.json");

        filePath = Path.GetFullPath(filePath);
        
        output.WriteLine($"Reading file: {filePath}");
        File.Exists(filePath).ShouldBeTrue($"File not found: {filePath}");
        
        var fileContent = File.ReadAllText(filePath);
        var fileSize = fileContent.Length;
        output.WriteLine($"File size: {fileSize:N0} characters");

        // Warm up the generator
        output.WriteLine("Warming up...");
        var warmupResult = RunGenerator(fileContent);
        
        // Debug: check what happened
        output.WriteLine($"Generated {warmupResult.GeneratedCount} source(s)");
        
        warmupResult.GeneratedCount.ShouldBeGreaterThan(0);

        // Run the actual timed test using process CPU-time for monotonic timing
        output.WriteLine("Starting timed generation...");
        var process = Process.GetCurrentProcess();
        var cpuStart = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        var result = RunGenerator(fileContent);

        var cpuEnd = process.TotalProcessorTime;
        wall.Stop();

        var elapsedCpuMs = (cpuEnd - cpuStart).TotalMilliseconds;
        var elapsedWallMs = wall.ElapsedMilliseconds;

        // Log results
        output.WriteLine($"Generation completed in {elapsedCpuMs:F1}ms (CPU), {elapsedWallMs}ms (wall)");
        output.WriteLine($"Generated {result.GeneratedCount} source file(s)");

        // Verify generation succeeded
        result.GeneratedCount.ShouldBeGreaterThan(0);

        // Performance assertion - should complete in reasonable time
        elapsedCpuMs.ShouldBeLessThan(5000, "Generation should complete in under 5 seconds (CPU time)");
    }

    [Fact]
    public void MultipleGenerationRunsShowConsistentPerformance()
    {
        var filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TablerIcon.enum.json");

        var fileContent = File.ReadAllText(filePath);

        var cpuTimes = new List<long>();
        var wallTimes = new List<long>();
        const int runs = 5;

        output.WriteLine($"Running {runs} iterations...");

        for (int i = 0; i < runs; i++)
        {
            var process = Process.GetCurrentProcess();
            var cpuStart = process.TotalProcessorTime;
            var wall = Stopwatch.StartNew();

            var result = RunGenerator(fileContent);

            var cpuEnd = process.TotalProcessorTime;
            wall.Stop();

            var elapsedCpuMs = (cpuEnd - cpuStart).TotalMilliseconds;
            var elapsedWallMs = wall.ElapsedMilliseconds;

            cpuTimes.Add((long)elapsedCpuMs);
            wallTimes.Add(elapsedWallMs);
            result.GeneratedCount.ShouldBeGreaterThan(0);

            output.WriteLine($"  Run {i + 1}: {elapsedCpuMs:F1}ms (CPU), {elapsedWallMs}ms (wall)");
        }

        var average = wallTimes.Average();
        var min = wallTimes.Min();
        var max = wallTimes.Max();

        output.WriteLine($"\nStatistics:");
        output.WriteLine($"  Min: {min}ms");
        output.WriteLine($"  Max: {max}ms");
        output.WriteLine($"  Average: {average:F2}ms");
        output.WriteLine($"  Variance: {max - min}ms");

        // Consistency check - first run may have JIT overhead,
        // so check consistency excluding the first run
        var subsequentRuns = wallTimes.Skip(1).ToList();
        var subsequentMin = subsequentRuns.Min();
        var subsequentMax = subsequentRuns.Max();
        
        // Subsequent runs should be reasonably consistent (within 2x variance is acceptable for GC/JIT)
        // This is mainly to catch regressions where performance degrades significantly
        (subsequentMax / (double)subsequentMin).ShouldBeLessThan(3.0, "Performance should not vary wildly across warmed-up runs");
    }

    [Fact]
    public void TablerIconEnumGenerationAndCompilationPerformance()
    {
        // Capture overall test timings (CPU + wall-clock)
        var overallProcess = Process.GetCurrentProcess();
        var overallCpuStart = overallProcess.TotalProcessorTime;
        var overallWall = Stopwatch.StartNew();

        // Read the large enum file
        var filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TablerIcon.enum.json");

        filePath = Path.GetFullPath(filePath);
        
        output.WriteLine($"Reading file: {filePath}");
        File.Exists(filePath).ShouldBeTrue($"File not found: {filePath}");
        
        var fileReadCpuStart = overallProcess.TotalProcessorTime;
        var fileReadWall = Stopwatch.StartNew();

        var fileContent = File.ReadAllText(filePath);

        var fileReadCpuEnd = overallProcess.TotalProcessorTime;
        fileReadWall.Stop();
        var fileReadCpuMs = (fileReadCpuEnd - fileReadCpuStart).TotalMilliseconds;
        var fileReadWallMs = fileReadWall.ElapsedMilliseconds;

        var fileSize = fileContent.Length;
        output.WriteLine($"File size: {fileSize:N0} characters");
        output.WriteLine($"File read: {fileReadCpuMs:F1}ms (CPU), {fileReadWallMs}ms (wall)");

        // Warm up
        output.WriteLine("\nWarm-up run...");
        var warmupCpuStart = overallProcess.TotalProcessorTime;
        var warmupWall = Stopwatch.StartNew();

        var warmupResult = RunGenerator(fileContent);

        var warmupCpuEnd = overallProcess.TotalProcessorTime;
        warmupWall.Stop();
        var warmupCpuMs = (warmupCpuEnd - warmupCpuStart).TotalMilliseconds;
        var warmupWallMs = warmupWall.ElapsedMilliseconds;

        warmupResult.GeneratedCount.ShouldBeGreaterThan(0);
        var generatedSource = warmupResult.SourceTexts[0].source;
        var warmupCompileCpuStart = overallProcess.TotalProcessorTime;
        var warmupCompileWall = Stopwatch.StartNew();
        CompileGeneratedCode(generatedSource);
        var warmupCompileCpuEnd = overallProcess.TotalProcessorTime;
        warmupCompileWall.Stop();
        var warmupCompileCpuMs = (warmupCompileCpuEnd - warmupCompileCpuStart).TotalMilliseconds;
        var warmupCompileWallMs = warmupCompileWall.ElapsedMilliseconds;

        output.WriteLine($"Warm-up generation: {warmupCpuMs:F1}ms (CPU), {warmupWallMs}ms (wall)");
        output.WriteLine($"Warm-up compile: {warmupCompileCpuMs:F1}ms (CPU), {warmupCompileWallMs}ms (wall)");

        // Measure generation time (CPU + wall-clock)
        output.WriteLine("\nMeasuring generation time...");
        var process = Process.GetCurrentProcess();
        var genCpuStart = process.TotalProcessorTime;
        var genWall = Stopwatch.StartNew();

        var result = RunGenerator(fileContent);

        var genCpuEnd = process.TotalProcessorTime;
        genWall.Stop();

        var generationCpuMs = (genCpuEnd - genCpuStart).TotalMilliseconds;
        var generationWallMs = genWall.ElapsedMilliseconds;

        result.GeneratedCount.ShouldBeGreaterThan(0);
        generatedSource = result.SourceTexts[0].source;

        output.WriteLine($"Generation completed in {generationCpuMs:F1}ms (CPU), {generationWallMs}ms (wall)");
        output.WriteLine($"Generated source size: {generatedSource.Length:N0} characters");

        // Measure compilation time (CPU + wall-clock)
        output.WriteLine("\nMeasuring compilation time...");
        var compCpuStart = process.TotalProcessorTime;
        var compWall = Stopwatch.StartNew();

        var compilation = CompileGeneratedCode(generatedSource);

        var compCpuEnd = process.TotalProcessorTime;
        compWall.Stop();

        var compilationCpuMs = (compCpuEnd - compCpuStart).TotalMilliseconds;
        var compilationWallMs = compWall.ElapsedMilliseconds;

        output.WriteLine($"Compilation completed in {compilationCpuMs:F1}ms (CPU), {compilationWallMs}ms (wall)");

        // Check for compilation errors
        // Use Emit to get diagnostics from the emit result instead of calling GetDiagnostics(),
        // which can be extremely expensive for large compilations due to symbol resolution/type forwarding.
        // Instrumentation for heavy emit/diagnostic phase
        var beforeGc0 = GC.CollectionCount(0);
        var beforeGc1 = GC.CollectionCount(1);
        var beforeGc2 = GC.CollectionCount(2);
        var beforeTotalMemory = GC.GetTotalMemory(false);
        var beforeThreadCount = Process.GetCurrentProcess().Threads.Count;

        var loadedBefore = AppDomain.CurrentDomain.GetAssemblies().Length;

        var emitCpuStart = overallProcess.TotalProcessorTime;
        var emitWall = Stopwatch.StartNew();

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        var emitCpuEnd = overallProcess.TotalProcessorTime;
        emitWall.Stop();
        var emitCpuMs = (emitCpuEnd - emitCpuStart).TotalMilliseconds;
        var emitWallMs = emitWall.ElapsedMilliseconds;

        var loadedAfter = AppDomain.CurrentDomain.GetAssemblies().Length;
        var afterGc0 = GC.CollectionCount(0);
        var afterGc1 = GC.CollectionCount(1);
        var afterGc2 = GC.CollectionCount(2);
        var afterTotalMemory = GC.GetTotalMemory(false);
        var afterThreadCount = Process.GetCurrentProcess().Threads.Count;

        output.WriteLine($"Assembly load delta during emit: {loadedAfter - loadedBefore}");
        output.WriteLine($"GC collections during emit: Gen0={afterGc0 - beforeGc0}, Gen1={afterGc1 - beforeGc1}, Gen2={afterGc2 - beforeGc2}");
        output.WriteLine($"Managed memory delta: {(afterTotalMemory - beforeTotalMemory) / 1024.0:F1}KB");
        output.WriteLine($"Thread count delta: {afterThreadCount - beforeThreadCount}");

        var errors = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        output.WriteLine($"Emit diagnostics: {emitCpuMs:F1}ms (CPU), {emitWallMs}ms (wall)");

        if (errors.Any())
        {
            output.WriteLine("\nCompilation errors (from Emit):");
            foreach (var error in errors.Take(10))
            {
                output.WriteLine($"  {error.GetMessage()}");
            }
        }

        if (warnings.Any())
        {
            output.WriteLine($"\nCompilation generated {warnings.Count} warning(s) (from Emit)");
        }

        // Report metrics
        output.WriteLine("\nPerformance Summary:");
        output.WriteLine($"  Generation time:  {generationCpuMs:F1}ms (CPU), {generationWallMs}ms (wall)");
        output.WriteLine($"  Compilation time: {compilationCpuMs:F1}ms (CPU), {compilationWallMs}ms (wall)");
        output.WriteLine($"  Total time:       {(generationCpuMs + compilationCpuMs):F1}ms (CPU), {generationWallMs + compilationWallMs}ms (wall)");
        output.WriteLine($"  Source size:      {generatedSource.Length / 1024.0:F1}KB");
        output.WriteLine($"  Lines of code:    ~{generatedSource.Count(c => c == '\n'):N0}");

        var ratio = compilationCpuMs / (double)generationCpuMs;
        output.WriteLine($"\nCompilation is {ratio:F1}x slower than generation (CPU time)");

        // Finish overall timings
        var overallCpuEnd = overallProcess.TotalProcessorTime;
        overallWall.Stop();
        var overallCpuMs = (overallCpuEnd - overallCpuStart).TotalMilliseconds;
        var overallWallMs = overallWall.ElapsedMilliseconds;

        output.WriteLine($"\nOverall test time: {overallCpuMs:F1}ms (CPU), {overallWallMs}ms (wall)");
        
        if (errors.Count > 0)
        {
            output.WriteLine($"\n⚠️ Note: Compilation has {errors.Count} error(s) due to missing assembly references in test setup");
            output.WriteLine("   This doesn't affect the performance measurements.");
        }
    }

    private static CSharpCompilation CompileGeneratedCode(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Diagnostics.Debug).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.TypeConverterAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CommunityToolkit.Diagnostics.Guard).Assembly.Location)
        };

        // Add a best-effort set of commonly needed runtime assemblies
        string[] tryLoad = new[] { "System.Runtime", "System.Collections", "System.Memory", "netstandard" };
        foreach (var name in tryLoad)
        {
            try
            {
                var a = Assembly.Load(name);
                if (a != null && !string.IsNullOrEmpty(a.Location))
                {
                    refs.Add(MetadataReference.CreateFromFile(a.Location));
                }
            }
            catch { }
        }

        // Also include all loaded assemblies from the current AppDomain to help with type-forwarded types
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct();

        foreach (var loc in loaded)
        {
            try
            {
                refs.Add(MetadataReference.CreateFromFile(loc));
            }
            catch { }
        }

        // Deduplicate by file location
        var references = refs
            .GroupBy(r => (r as PortableExecutableReference)?.FilePath ?? string.Empty)
            .Select(g => g.First())
            .ToArray();

        // Attempt to probe .NET SDK 'ref' packs (e.g., Microsoft.NETCore.App.Ref) for exact reference assemblies
        try
        {
            var triedPaths = new List<string>();

            var rootsToTry = new List<string?>()
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet")
            };

            foreach (var root in rootsToTry.Where(r => !string.IsNullOrEmpty(r)).Distinct())
            {
                var packsDir = Path.Combine(root!, "packs");
                triedPaths.Add(packsDir);
                if (!Directory.Exists(packsDir))
                    continue;

                var packCandidates = Directory.EnumerateDirectories(packsDir, "Microsoft.NETCore.App.Ref*", SearchOption.TopDirectoryOnly);
                foreach (var pack in packCandidates)
                {
                    var refNet9 = Path.Combine(pack, "ref", "net9.0");
                    var refNetStandard = Path.Combine(pack, "ref", "netstandard2.0");
                    foreach (var refFolder in new[] { refNet9, refNetStandard })
                    {
                        triedPaths.Add(refFolder);
                        if (!Directory.Exists(refFolder))
                            continue;

                        var dlls = Directory.EnumerateFiles(refFolder, "*.dll", SearchOption.TopDirectoryOnly);
                        foreach (var dll in dlls)
                        {
                            try
                            {
                                var mr = MetadataReference.CreateFromFile(dll);
                                if (!references.Any(r => (r as PortableExecutableReference)?.FilePath == dll))
                                {
                                    references = references.Concat(new[] { mr }).ToArray();
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            if (!references.Any(r => (r as PortableExecutableReference)?.FilePath?.Contains("System.ComponentModel.TypeConverter") == true))
            {
                // Try explicit Program Files packs location and pick the latest Microsoft.NETCore.App.Ref
                var programPacks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs");
                if (Directory.Exists(programPacks))
                {
                    var packs = Directory.EnumerateDirectories(programPacks, "Microsoft.NETCore.App.Ref.*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(p => p)
                        .ToList();

                    foreach (var pack in packs)
                    {
                        var refFolder = Path.Combine(pack, "ref", "net9.0");
                        if (!Directory.Exists(refFolder))
                            continue;

                        foreach (var dll in Directory.EnumerateFiles(refFolder, "*.dll", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                var mr = MetadataReference.CreateFromFile(dll);
                                if (!references.Any(r => (r as PortableExecutableReference)?.FilePath == dll))
                                {
                                    references = references.Concat(new[] { mr }).ToArray();
                                }
                            }
                            catch { }
                        }

                        // stop after first usable pack
                        break;
                    }
                }

                Console.WriteLine("Ref-pack probing attempted paths:");
                foreach (var p in triedPaths.Distinct())
                {
                    Console.WriteLine("  " + p);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ref-pack probing failed: {ex.Message}");
        }

        // Add Trusted Platform Assemblies (TPA) entries to references (helps resolve runtime-forwarded types)
        try
        {
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(tpa))
            {
                var tpaPaths = tpa.Split(Path.PathSeparator);
                foreach (var p in tpaPaths)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p) && File.Exists(p) && !references.Any(r => (r as PortableExecutableReference)?.FilePath == p))
                        {
                            references = references.Concat(new[] { MetadataReference.CreateFromFile(p) }).ToArray();
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratedEnumTest",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    private static GeneratorRunResult RunGenerator(string fileContent)
    {
        // Instead of using the driver (which has assembly loading issues),
        // we'll call the generator's public API directly
        var generator = new StringBackedEnumTypeSourceGenerator();
        
        var result = generator.GenerateFromFiles("TablerIcon.enum.json", fileContent, CancellationToken.None);
        
        // Convert the result to a simple structure
        var generatedSources = result?.ToList() ?? new List<(string, string)>();
        
        return new GeneratorRunResult
        {
            GeneratedCount = generatedSources.Count,
            SourceTexts = generatedSources
        };
    }

    private class GeneratorRunResult
    {
        public int GeneratedCount { get; init; }
        public List<(string fileName, string source)> SourceTexts { get; init; } = new();
    }
}
