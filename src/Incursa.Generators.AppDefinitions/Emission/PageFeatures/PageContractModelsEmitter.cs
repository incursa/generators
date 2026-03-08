namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class PageContractModelsEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-contract-models";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            var relativePath = EmitterUtilities.BuildRelativePath(target, feature.RelativeDirectory, $"{feature.Name}Contracts.g.cs");
            var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
            var fileNamespace = EmitterUtilities.BuildFeatureNamespace(target, feature);
            var builder = new CodeBuilder();

            EmitterUtilities.AppendHeader(builder, target.Name, Kind, relativePath);
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Text.Json.Serialization;");
            builder.AppendLine();
            builder.AppendLine($"namespace {fileNamespace};");
            builder.AppendLine();

            EmitModelClass(builder, feature.ViewModelTypeName, null, feature.ViewModelProperties);

            foreach (var ownedType in feature.OwnedTypes)
            {
                builder.AppendLine();
                EmitModelClass(builder, ownedType.Name, ownedType.Inherits, ownedType.Properties);
            }

            foreach (var apiModel in feature.ApiModels)
            {
                builder.AppendLine();
                EmitModelClass(builder, apiModel.Name, apiModel.Inherits, apiModel.Properties);
            }

            generatedFiles.Add(new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString()));
        }

        return generatedFiles;
    }

    private static void EmitModelClass(CodeBuilder builder, string className, string? inherits, IReadOnlyList<PropertyDefinition> properties)
    {
        var inheritanceClause = string.IsNullOrWhiteSpace(inherits) ? string.Empty : $" : {inherits}";
        builder.AppendLine($"public partial class {className}{inheritanceClause}");
        builder.AppendLine("{");
        using (builder.Indent())
        {
            if (properties.Count == 0)
            {
                builder.AppendLine("// No properties defined.");
            }
            else
            {
                foreach (var property in properties)
                {
                    if (!string.IsNullOrWhiteSpace(property.JsonName))
                    {
                        builder.AppendLine($"[JsonPropertyName(\"{property.JsonName}\")]");
                    }

                    builder.AppendLine(EmitterUtilities.BuildPropertyDeclaration(property, useInitAccessor: true));
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine("}");
    }
}
