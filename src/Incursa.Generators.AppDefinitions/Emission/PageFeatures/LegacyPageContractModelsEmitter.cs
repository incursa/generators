namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class LegacyPageContractModelsEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-contract-models-legacy";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            generatedFiles.Add(EmitTypeFile(target, model, feature, feature.ViewModelTypeName, feature.ViewModelProperties, currentNamespace: target.Namespace));

            var featureNamespace = LegacyPageFeatureUtilities.GetContractFeatureNamespace(target, feature);
            foreach (var ownedType in feature.OwnedTypes)
            {
                generatedFiles.Add(EmitTypeFile(target, model, feature, ownedType.Name, ownedType.Properties, featureNamespace));
            }

            foreach (var apiModel in feature.ApiModels)
            {
                generatedFiles.Add(EmitTypeFile(target, model, feature, apiModel.Name, apiModel.Properties, featureNamespace));
            }
        }

        return generatedFiles;
    }

    private static GeneratedFile EmitTypeFile(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        PageFeatureDefinition feature,
        string typeName,
        IReadOnlyList<PropertyDefinition> properties,
        string currentNamespace)
    {
        var relativeDirectory = string.IsNullOrWhiteSpace(feature.RelativeDirectory)
            ? feature.Name
            : $"{EmitterUtilities.NormalizeRelativePath(feature.RelativeDirectory)}/{feature.Name}";
        var relativePath = EmitterUtilities.BuildRelativePath(target, relativeDirectory, $"{typeName}.g.cs");
        var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
        var builder = new CodeBuilder();

        LegacyPageFeatureUtilities.AppendLegacySourceHeader(builder, model, feature, enableNullable: true);
        builder.AppendLine($"namespace {currentNamespace};");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.ComponentModel.DataAnnotations;");
        builder.AppendLine("using System.Text.Json.Serialization;");
        builder.AppendLine("using FluentValidation;");
        builder.AppendLine();
        builder.AppendLine($"public partial record {typeName}");
        builder.AppendLine("{");
        using (builder.Indent())
        {
            builder.AppendLine();

            foreach (var property in properties)
            {
                LegacyPageFeatureUtilities.AppendLegacyPropertyDocumentation(builder, property);
                if (property.Required)
                {
                    builder.AppendLine("[Required]");
                }

                if (!string.IsNullOrWhiteSpace(property.JsonName))
                {
                    builder.AppendLine($"[JsonPropertyName(\"{property.JsonName}\")]");
                }

                var rewrittenType = LegacyPageFeatureUtilities.GetLegacyContractTypeName(target, feature, EmitterUtilities.FormatTypeName(property.Type, property.Nullable));
                builder.AppendLine(LegacyPageFeatureUtilities.BuildLegacyPropertyDeclaration(property, rewrittenType));
                builder.AppendLine();
            }

            builder.AppendLine("// Static validator instance");
            builder.AppendLine($"public static readonly {typeName}Validator Validator = new {typeName}Validator();");
            builder.AppendLine();
            builder.AppendLine("// Instance method for validation");
            builder.AppendLine("public void Validate()");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                builder.AppendLine("var validationResult = Validator.Validate(this);");
                builder.AppendLine("if (!validationResult.IsValid)");
                builder.AppendLine("{");
                using (builder.Indent())
                {
                    builder.AppendLine("throw new FluentValidation.ValidationException(validationResult.ToString());");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("// Method to check validation without throwing exceptions");
            builder.AppendLine("public FluentValidation.Results.ValidationResult GetValidationResult()");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                builder.AppendLine("return Validator.Validate(this);");
            }

            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("    // Internal FluentValidator class for validation rules");
            builder.AppendLine($"    public partial class {typeName}Validator : AbstractValidator<{typeName}>");
            builder.AppendLine("    {");
            builder.AppendLine($"        public {typeName}Validator()");
            builder.AppendLine("        {");
            foreach (var property in properties.Where(static property => property.Required))
            {
                builder.AppendLine($"    RuleFor(x => x.{property.Name}).NotNull().WithMessage(\"'{property.Name}' is required.\");");
                builder.AppendLine();
            }

            builder.AppendLine("                // Hooks for additional validation");
            builder.AppendLine("                AddCustomValidation();");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        // Partial method for additional validation hooks");
            builder.AppendLine("        partial void AddCustomValidation();");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return new GeneratedFile(target.Name, "page-contract-models-legacy", absolutePath, relativePath, builder.ToString());
    }
}
