namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class PageRegistrationHelperEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-registration-helper";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(filter.RawPattern))
        {
            return [];
        }

        var relativePath = "PageFeatureRegistrationExtensions.g.cs";
        var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
        var uiEngineNamespace = EmitterUtilities.ResolveImportNamespace(target, "uiEngines");
        var builder = new CodeBuilder();

        EmitterUtilities.AppendHeader(builder, target.Name, Kind, relativePath);
        builder.AppendLine("using System;");
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        if (!string.Equals(uiEngineNamespace, target.Namespace, StringComparison.Ordinal))
        {
            builder.AppendLine($"using {uiEngineNamespace};");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {target.Namespace};");
        builder.AppendLine();
        builder.AppendLine("public static class PageFeatureRegistrationExtensions");
        builder.AppendLine("{");
        using (builder.Indent())
        {
            var features = model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal).ToArray();
            if (features.Length == 0)
            {
                builder.AppendLine("// No matching features were selected.");
            }
            else
            {
                foreach (var feature in features)
                {
                    builder.AppendLine($"public static IServiceCollection Add{feature.Name}UiEngine<TImplementation>(this IServiceCollection services)");
                    builder.AppendLine($"    where TImplementation : class, I{feature.Name}UiEngine");
                    builder.AppendLine("{");
                    using (builder.Indent())
                    {
                        builder.AppendLine("ArgumentNullException.ThrowIfNull(services);");
                        builder.AppendLine($"services.AddScoped<I{feature.Name}UiEngine, TImplementation>();");
                        builder.AppendLine("return services;");
                    }

                    builder.AppendLine("}");
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine("}");
        return [new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString())];
    }
}
