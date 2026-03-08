namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class PageRouteHelperEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-route-helper";

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

        var relativePath = "PageRoutes.g.cs";
        var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
        var builder = new CodeBuilder();

        EmitterUtilities.AppendHeader(builder, target.Name, Kind, relativePath);
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine($"namespace {target.Namespace};");
        builder.AppendLine();
        builder.AppendLine("public static class PageRoutes");
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
                    builder.AppendLine($"public const string {feature.Name}Route = \"{BuildRouteTemplate(feature)}\";");
                }

                builder.AppendLine();

                foreach (var feature in features)
                {
                    EmitPathBuilder(builder, feature);
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine("}");
        return [new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString())];
    }

    private static void EmitPathBuilder(CodeBuilder builder, PageFeatureDefinition feature)
    {
        var pageParameters = feature.PageParameters
            .Where(static parameter => parameter.Source is PageParameterSource.Route or PageParameterSource.Query)
            .Select(static parameter => EmitterUtilities.CreateMethodParameter(parameter.Type, parameter.Name, parameter.Required))
            .ToArray();

        builder.AppendLine($"public static string Get{feature.Name}Path({EmitterUtilities.FormatMethodSignature(pageParameters, includeCancellationToken: false)})");
        builder.AppendLine("{");
        using (builder.Indent())
        {
            builder.AppendLine($"var path = {feature.Name}Route;");

            foreach (var routeParameter in feature.PageParameters.Where(static parameter => parameter.Source == PageParameterSource.Route))
            {
                builder.AppendLine(
                    $"path = path.Replace(\"{{{routeParameter.Name}}}\", Uri.EscapeDataString({routeParameter.Name}.ToString() ?? string.Empty), StringComparison.Ordinal);");
            }

            var queryParameters = feature.PageParameters.Where(static parameter => parameter.Source == PageParameterSource.Query).ToArray();
            if (queryParameters.Length > 0)
            {
                builder.AppendLine("var queryParameters = new List<string>();");
                foreach (var queryParameter in queryParameters)
                {
                    if (queryParameter.Required)
                    {
                        builder.AppendLine(
                            $"queryParameters.Add(\"{queryParameter.Name}=\" + Uri.EscapeDataString({queryParameter.Name}.ToString() ?? string.Empty));");
                    }
                    else
                    {
                        builder.AppendLine($"if ({queryParameter.Name} is not null)");
                        builder.AppendLine("{");
                        using (builder.Indent())
                        {
                            builder.AppendLine(
                                $"queryParameters.Add(\"{queryParameter.Name}=\" + Uri.EscapeDataString({queryParameter.Name}.ToString() ?? string.Empty));");
                        }

                        builder.AppendLine("}");
                    }
                }

                builder.AppendLine("if (queryParameters.Count > 0)");
                builder.AppendLine("{");
                using (builder.Indent())
                {
                    builder.AppendLine("path += \"?\" + string.Join(\"&\", queryParameters);");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("return path;");
        }

        builder.AppendLine("}");
    }

    private static string BuildRouteTemplate(PageFeatureDefinition feature)
    {
        var route = string.IsNullOrWhiteSpace(feature.Route) ? feature.Name : feature.Route;
        return "/" + route.TrimStart('/');
    }
}
