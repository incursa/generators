namespace Incursa.Generators.AppDefinitions.Pipeline;

using System.Text;
using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Emission;
using Incursa.Generators.AppDefinitions.Emission.PageFeatures;
using Incursa.Generators.AppDefinitions.Input;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Validation;

public sealed class AppDefinitionGenerator
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly GeneratorConfigLoader configLoader = new();
    private readonly AppDefinitionParser parser = new();
    private readonly AppDefinitionValidator validator = new();
    private readonly IReadOnlyDictionary<string, IGenerationTargetEmitter> emitters = new Dictionary<string, IGenerationTargetEmitter>(StringComparer.OrdinalIgnoreCase)
    {
        ["page-contract-models"] = new PageContractModelsEmitter(),
        ["page-ui-engine-interface"] = new PageUiEngineInterfaceEmitter(),
        ["page-model-base"] = new PageModelBaseEmitter(),
        ["page-registration-helper"] = new PageRegistrationHelperEmitter(),
    };

    public GenerationResult Execute(GenerationRequest request, GenerationExecutionMode mode)
    {
        var diagnostics = new DiagnosticBag();
        var config = configLoader.Load(request.ConfigPath, diagnostics);
        if (config is null)
        {
            return new GenerationResult(mode, diagnostics.Items, [], 0, 0, 0, 0, 0);
        }

        if (!string.IsNullOrWhiteSpace(request.DefinitionsPathOverride))
        {
            config = config with { DefinitionRoot = Path.GetFullPath(request.DefinitionsPathOverride) };
        }

        if (mode != GenerationExecutionMode.Validate && config.Targets.Count == 0)
        {
            diagnostics.AddError("APPDEF031", "Generate mode requires at least one configured output target.", SourceLocation.FromFile(config.ConfigFilePath));
        }

        var model = parser.Parse(config.DefinitionRoot, config.DefinitionPatterns, diagnostics);
        validator.Validate(config, model, diagnostics);

        var filter = FeatureFilter.Create(request.FilterPattern);
        var allowOwnershipCleanup = string.IsNullOrWhiteSpace(request.FilterPattern);
        var matchedFeatures = model.PageFeatures.Where(filter.IsMatch).ToArray();
        if (!string.IsNullOrWhiteSpace(request.FilterPattern) && matchedFeatures.Length == 0)
        {
            diagnostics.AddWarning("APPDEF032", $"Filter '{request.FilterPattern}' matched no features.", SourceLocation.FromFile(config.ConfigFilePath));
        }

        if (!allowOwnershipCleanup && mode != GenerationExecutionMode.Validate)
        {
            diagnostics.AddInfo("APPDEF040", "Filtered generation skips orphan cleanup and manifest updates for safety.", SourceLocation.FromFile(config.ConfigFilePath));
        }

        if (mode == GenerationExecutionMode.Validate || diagnostics.HasErrors)
        {
            return new GenerationResult(mode, diagnostics.Items, [], model.PageFeatures.Count, matchedFeatures.Length, 0, 0, 0);
        }

        var filteredModel = new ApplicationDefinitionSet(model.DefinitionRootPath, matchedFeatures);
        var generatedFiles = EmitFiles(config, filteredModel, filter, diagnostics);

        if (diagnostics.HasErrors)
        {
            return new GenerationResult(mode, diagnostics.Items, generatedFiles, model.PageFeatures.Count, matchedFeatures.Length, 0, 0, 0);
        }

        var synchronization = SynchronizeOutputs(config, generatedFiles, diagnostics, mode == GenerationExecutionMode.Write, allowOwnershipCleanup);
        return new GenerationResult(
            mode,
            diagnostics.Items,
            generatedFiles,
            model.PageFeatures.Count,
            matchedFeatures.Length,
            synchronization.FilesWritten,
            synchronization.FilesDeleted,
            synchronization.FilesUnchanged);
    }

    private IReadOnlyList<GeneratedFile> EmitFiles(
        ResolvedGeneratorConfig config,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();
        var absolutePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in config.Targets.OrderBy(static target => target.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!emitters.TryGetValue(target.Kind, out var emitter))
            {
                diagnostics.AddError("APPDEF033", $"Target '{target.Name}' uses unsupported kind '{target.Kind}'.", SourceLocation.FromFile(config.ConfigFilePath));
                continue;
            }

            foreach (var file in emitter.Emit(target, model, filter, diagnostics))
            {
                if (!absolutePaths.Add(file.AbsolutePath))
                {
                    diagnostics.AddError("APPDEF034", $"Multiple targets would generate the same output path '{file.AbsolutePath}'.", SourceLocation.FromFile(file.AbsolutePath));
                    continue;
                }

                generatedFiles.Add(file with { Content = file.Content.Replace("\r\n", "\n", StringComparison.Ordinal) });
            }
        }

        return generatedFiles.OrderBy(static file => file.AbsolutePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private SynchronizationSummary SynchronizeOutputs(
        ResolvedGeneratorConfig config,
        IReadOnlyList<GeneratedFile> generatedFiles,
        DiagnosticBag diagnostics,
        bool writeChanges,
        bool allowOwnershipCleanup)
    {
        var expectedPaths = generatedFiles.Select(static file => file.AbsolutePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filesWritten = 0;
        var filesDeleted = 0;
        var filesUnchanged = 0;

        foreach (var generatedFile in generatedFiles)
        {
            var existingContent = File.Exists(generatedFile.AbsolutePath)
                ? File.ReadAllText(generatedFile.AbsolutePath).Replace("\r\n", "\n", StringComparison.Ordinal)
                : null;

            if (string.Equals(existingContent, generatedFile.Content, StringComparison.Ordinal))
            {
                filesUnchanged++;
                continue;
            }

            if (!writeChanges)
            {
                diagnostics.AddError("APPDEF035", $"Generated file '{generatedFile.AbsolutePath}' is missing or out of date.", SourceLocation.FromFile(generatedFile.AbsolutePath));
                continue;
            }

            var directory = Path.GetDirectoryName(generatedFile.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(generatedFile.AbsolutePath, generatedFile.Content, Utf8WithoutBom);
            filesWritten++;
        }

        if (!allowOwnershipCleanup)
        {
            return new SynchronizationSummary(filesWritten, filesDeleted, filesUnchanged);
        }

        foreach (var target in config.Targets)
        {
            var manifestPath = GeneratedOutputManifestStore.GetManifestPath(target);
            var expectedFilesForTarget = generatedFiles
                .Where(file => string.Equals(file.TargetName, target.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var expectedRelativePaths = expectedFilesForTarget
                .Select(file => EmitterUtilities.NormalizeRelativePath(file.RelativePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var errorCountBeforeManifest = diagnostics.ErrorCount;
            var manifest = GeneratedOutputManifestStore.TryLoad(target, diagnostics);
            var manifestHasErrors = diagnostics.ErrorCount > errorCountBeforeManifest;
            if (manifestHasErrors)
            {
                continue;
            }

            var orphanRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manifest is not null)
            {
                foreach (var orphan in manifest.Files.Except(expectedRelativePaths, StringComparer.OrdinalIgnoreCase))
                {
                    orphanRelativePaths.Add(orphan);
                }
            }

            if (Directory.Exists(target.Directory))
            {
                foreach (var existingFile in Directory.EnumerateFiles(target.Directory, "*.g.cs", SearchOption.AllDirectories))
                {
                    var fullPath = Path.GetFullPath(existingFile);
                    if (expectedPaths.Contains(fullPath))
                    {
                        continue;
                    }

                    var relativePath = EmitterUtilities.NormalizeRelativePath(Path.GetRelativePath(target.Directory, fullPath));
                    var content = File.ReadAllText(fullPath);
                    if (EmitterUtilities.IsOwnedGeneratedFile(content, target.Name, target.Kind, relativePath))
                    {
                        orphanRelativePaths.Add(relativePath);
                    }
                }
            }

            var errorCountBeforeOrphanProcessing = diagnostics.ErrorCount;
            foreach (var orphanRelativePath in orphanRelativePaths.OrderBy(static path => path, StringComparer.Ordinal))
            {
                var orphanAbsolutePath = Path.GetFullPath(Path.Combine(target.Directory, orphanRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(orphanAbsolutePath))
                {
                    continue;
                }

                var orphanContent = File.ReadAllText(orphanAbsolutePath);
                if (!EmitterUtilities.IsOwnedGeneratedFile(orphanContent, target.Name, target.Kind, orphanRelativePath))
                {
                    diagnostics.AddError("APPDEF041", $"File '{orphanAbsolutePath}' is listed as orphaned for target '{target.Name}' but is not owned by this tool anymore. Remove it manually.", SourceLocation.FromFile(orphanAbsolutePath));
                    continue;
                }

                if (!writeChanges)
                {
                    diagnostics.AddError("APPDEF036", $"Generated file '{orphanAbsolutePath}' is stale and should be removed.", SourceLocation.FromFile(orphanAbsolutePath));
                    continue;
                }

                File.Delete(orphanAbsolutePath);
                filesDeleted++;
            }

            if (diagnostics.ErrorCount > errorCountBeforeOrphanProcessing)
            {
                continue;
            }

            var nextManifest = GeneratedOutputManifestStore.Create(target, expectedFilesForTarget);
            var nextManifestContent = GeneratedOutputManifestStore.Serialize(nextManifest);
            var existingManifestContent = File.Exists(manifestPath)
                ? File.ReadAllText(manifestPath).Replace("\r\n", "\n", StringComparison.Ordinal)
                : null;

            if (string.Equals(existingManifestContent, nextManifestContent, StringComparison.Ordinal))
            {
                filesUnchanged++;
                continue;
            }

            if (!writeChanges)
            {
                diagnostics.AddError("APPDEF042", $"Ownership manifest '{manifestPath}' is missing or out of date.", SourceLocation.FromFile(manifestPath));
                continue;
            }

            Directory.CreateDirectory(target.Directory);
            File.WriteAllText(manifestPath, nextManifestContent, Utf8WithoutBom);
            filesWritten++;
        }

        return new SynchronizationSummary(filesWritten, filesDeleted, filesUnchanged);
    }

    private sealed record SynchronizationSummary(int FilesWritten, int FilesDeleted, int FilesUnchanged);
}
