namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class PageUiEngineInterfaceEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-ui-engine-interface";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            var relativePath = EmitterUtilities.BuildRelativePath(target, feature.RelativeDirectory, $"I{feature.Name}UiEngine.g.cs");
            var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
            var fileNamespace = EmitterUtilities.BuildNamespace(target, feature.RelativeDirectory);
            var contractNamespace = EmitterUtilities.ResolveImportNamespace(target, "contracts");

            var builder = new CodeBuilder();
            EmitterUtilities.AppendHeader(builder, target.Name, Kind, relativePath);
            builder.AppendLine("using System.Threading;");
            builder.AppendLine("using System.Threading.Tasks;");
            if (!string.Equals(contractNamespace, fileNamespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {contractNamespace};");
            }

            builder.AppendLine();
            builder.AppendLine($"namespace {fileNamespace};");
            builder.AppendLine();
            builder.AppendLine($"public interface I{feature.Name}UiEngine");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                if (feature.Operations.Count == 0)
                {
                    builder.AppendLine("// No operations defined.");
                }
                else
                {
                    foreach (var operation in feature.Operations)
                    {
                        var resultType = EmitterUtilities.GetOperationResultType(feature, operation);
                        var returnType = resultType is null ? "Task" : $"Task<{resultType}>";
                        var parameters = EmitterUtilities.BuildInterfaceMethodParameters(feature, operation);
                        builder.AppendLine($"{returnType} {EmitterUtilities.GetOperationMethodName(operation)}({EmitterUtilities.FormatMethodSignature(parameters, includeCancellationToken: true)});");
                    }
                }
            }

            builder.AppendLine("}");

            generatedFiles.Add(new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString()));
        }

        return generatedFiles;
    }
}
