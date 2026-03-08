namespace Incursa.Generators.AppDefinitions.Config;

using System.Text.Json;
using Incursa.Generators.AppDefinitions.Diagnostics;

public sealed class GeneratorConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public ResolvedGeneratorConfig? Load(string configPath, DiagnosticBag diagnostics)
    {
        var fullConfigPath = Path.GetFullPath(configPath);

        if (!File.Exists(fullConfigPath))
        {
            diagnostics.AddError("APPDEF001", $"Config file '{fullConfigPath}' does not exist.", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        GeneratorConfig? config;

        try
        {
            config = JsonSerializer.Deserialize<GeneratorConfig>(File.ReadAllText(fullConfigPath), SerializerOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.AddError("APPDEF002", $"Config file '{fullConfigPath}' is not valid JSON. {exception.Message}", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        if (config is null)
        {
            diagnostics.AddError("APPDEF003", $"Config file '{fullConfigPath}' did not deserialize to a generator configuration.", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        if (config.Version != 1)
        {
            diagnostics.AddError("APPDEF004", $"Unsupported config version '{config.Version}'. Only version 1 is supported.", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.DefinitionRoot))
        {
            diagnostics.AddError("APPDEF005", "Config property 'definitionRoot' is required.", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        var configDirectory = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();
        var definitionRoot = ResolvePath(configDirectory, config.DefinitionRoot);
        var definitionPatterns = (config.DefinitionPatterns is { Count: > 0 } ? config.DefinitionPatterns : ["*.xml"])
            .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (definitionPatterns.Length == 0)
        {
            diagnostics.AddError("APPDEF006", "Config must include at least one non-empty definition pattern.", SourceLocation.FromFile(fullConfigPath));
            return null;
        }

        var targets = new List<ResolvedOutputTarget>();
        foreach (var pair in config.Targets.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var target = pair.Value;
            if (string.IsNullOrWhiteSpace(target.Kind))
            {
                diagnostics.AddError("APPDEF007", $"Target '{pair.Key}' is missing required property 'kind'.", SourceLocation.FromFile(fullConfigPath));
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.Directory))
            {
                diagnostics.AddError("APPDEF008", $"Target '{pair.Key}' is missing required property 'directory'.", SourceLocation.FromFile(fullConfigPath));
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.Namespace))
            {
                diagnostics.AddError("APPDEF009", $"Target '{pair.Key}' is missing required property 'namespace'.", SourceLocation.FromFile(fullConfigPath));
                continue;
            }

            var namespaceMode = NormalizeNamespaceMode(target.NamespaceMode, pair.Key, fullConfigPath, diagnostics);
            if (namespaceMode is null)
            {
                continue;
            }

            targets.Add(new ResolvedOutputTarget(
                pair.Key,
                target.Kind,
                ResolvePath(configDirectory, target.Directory),
                target.Namespace,
                namespaceMode,
                string.IsNullOrWhiteSpace(target.BaseType) ? null : target.BaseType.Trim(),
                target.PreserveDefinitionFolders,
                target.AppendRelativePathToNamespace,
                new Dictionary<string, string>(target.Imports, StringComparer.OrdinalIgnoreCase)));
        }

        return new ResolvedGeneratorConfig(
            fullConfigPath,
            definitionRoot,
            definitionPatterns,
            new ValidationSettings
            {
                AllowUnqualifiedExternalTypes = config.Validation.AllowUnqualifiedExternalTypes,
                KnownTypeNames = config.Validation.KnownTypeNames
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray(),
            },
            targets);
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static string? NormalizeNamespaceMode(string? namespaceMode, string targetName, string configPath, DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(namespaceMode))
        {
            return "default";
        }

        if (string.Equals(namespaceMode, "default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(namespaceMode, "feature", StringComparison.OrdinalIgnoreCase))
        {
            return namespaceMode.ToLowerInvariant();
        }

        diagnostics.AddError(
            "APPDEF041",
            $"Target '{targetName}' uses unsupported namespaceMode '{namespaceMode}'. Supported values are 'default' and 'feature'.",
            SourceLocation.FromFile(configPath));
        return null;
    }
}
