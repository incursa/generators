namespace Incursa.Generators.AppDefinitions.Pipeline;

using System.Text.Json;
using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Emission;

internal sealed class GeneratedOutputManifest
{
    public int Version { get; init; } = 1;

    public string Tool { get; init; } = EmitterUtilities.ToolName;

    public string TargetName { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public IReadOnlyList<string> Files { get; init; } = [];
}

internal static class GeneratedOutputManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static string GetManifestFileName(string targetName) => $".incursa-appdefs.{targetName}.manifest.json";

    public static string GetManifestPath(ResolvedOutputTarget target) => Path.Combine(target.Directory, GetManifestFileName(target.Name));

    public static GeneratedOutputManifest Create(ResolvedOutputTarget target, IEnumerable<GeneratedFile> generatedFiles)
    {
        return new GeneratedOutputManifest
        {
            TargetName = target.Name,
            TargetKind = target.Kind,
            Files = generatedFiles
                .Where(file => string.Equals(file.TargetName, target.Name, StringComparison.OrdinalIgnoreCase))
                .Select(file => EmitterUtilities.NormalizeRelativePath(file.RelativePath))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public static string Serialize(GeneratedOutputManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, SerializerOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static GeneratedOutputManifest? TryLoad(
        ResolvedOutputTarget target,
        DiagnosticBag diagnostics)
    {
        var manifestPath = GetManifestPath(target);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<GeneratedOutputManifest>(File.ReadAllText(manifestPath));
            if (manifest is null)
            {
                diagnostics.AddError("APPDEF037", $"Ownership manifest '{manifestPath}' is empty or invalid.", SourceLocation.FromFile(manifestPath));
                return null;
            }

            if (manifest.Version != 1
                || !string.Equals(manifest.Tool, EmitterUtilities.ToolName, StringComparison.Ordinal)
                || !string.Equals(manifest.TargetName, target.Name, StringComparison.Ordinal)
                || !string.Equals(manifest.TargetKind, target.Kind, StringComparison.Ordinal))
            {
                diagnostics.AddError("APPDEF038", $"Ownership manifest '{manifestPath}' does not match the configured target.", SourceLocation.FromFile(manifestPath));
                return null;
            }

            return new GeneratedOutputManifest
            {
                Version = manifest.Version,
                Tool = manifest.Tool,
                TargetName = manifest.TargetName,
                TargetKind = manifest.TargetKind,
                Files = manifest.Files
                    .Select(EmitterUtilities.NormalizeRelativePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToArray(),
            };
        }
        catch (JsonException exception)
        {
            diagnostics.AddError("APPDEF039", $"Ownership manifest '{manifestPath}' is not valid JSON. {exception.Message}", SourceLocation.FromFile(manifestPath));
            return null;
        }
    }
}
