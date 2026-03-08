namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class LegacyPageUiEngineInterfaceEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-ui-engine-interface-legacy";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();
        var contractNamespace = EmitterUtilities.ResolveImportNamespace(target, "contracts");
        var contractTarget = new ResolvedOutputTarget(
            "contracts",
            "page-contract-models-legacy",
            target.Directory,
            contractNamespace,
            target.NamespaceMode,
            target.BaseType,
            true,
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            var relativePath = EmitterUtilities.BuildRelativePath(target, feature.RelativeDirectory, $"I{feature.Name}UiEngine.g.cs");
            var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
            var builder = new CodeBuilder();

            LegacyPageFeatureUtilities.AppendLegacySourceHeader(builder, model, feature, enableNullable: false);
            builder.AppendLine($"namespace {target.Namespace};");
            builder.AppendLine();
            builder.AppendLine("using System.Threading;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using PayeWaive;");
            if (!string.Equals(contractNamespace, target.Namespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {contractNamespace}; // Include VM namespace for types used in signatures");
            }

            builder.AppendLine();
            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// UI Engine for {feature.Name} feature.");
            builder.AppendLine("/// ");
            builder.AppendLine("/// </summary>");
            builder.AppendLine($"public interface I{feature.Name}UiEngine");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                var initVmParameters = LegacyPageFeatureUtilities.BuildLegacyUiEngineParameters(contractTarget, feature, operation: null);
                builder.AppendLine(
                    $"Task<{LegacyPageFeatureUtilities.GetLegacyOperationResultType(contractTarget, feature, operation: null)}> InitVmAsync({LegacyPageFeatureUtilities.FormatLegacyMethodSignature(initVmParameters)});");

                foreach (var operation in feature.Operations
                    .OrderBy(static operation => operation.Location.Line)
                    .ThenBy(static operation => operation.Location.Column)
                    .ThenBy(static operation => operation.Name, StringComparer.Ordinal))
                {
                    builder.AppendLine();
                    var resultType = LegacyPageFeatureUtilities.GetLegacyOperationResultType(contractTarget, feature, operation);
                    var returnType = string.Equals(resultType, "void", StringComparison.Ordinal)
                        ? "Task"
                        : LegacyPageFeatureUtilities.IsTaskLikeReturnType(resultType)
                            ? LegacyPageFeatureUtilities.NormalizeTaskLikeReturnType(resultType)
                            : $"Task<{resultType}>";
                    var parameters = LegacyPageFeatureUtilities.BuildLegacyUiEngineParameters(contractTarget, feature, operation);
                    builder.AppendLine($"{returnType} {EmitterUtilities.GetOperationMethodName(operation)}({LegacyPageFeatureUtilities.FormatLegacyMethodSignature(parameters)});");
                }
            }

            builder.AppendLine("}");
            generatedFiles.Add(new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString()));
        }

        return generatedFiles;
    }
}
