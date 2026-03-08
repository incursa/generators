namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;
using Incursa.Generators.AppDefinitions.Validation;

public sealed class PageModelBaseEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-model-base";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            var relativePath = EmitterUtilities.BuildRelativePath(target, feature.RelativeDirectory, $"{feature.Name}PageModelBase.g.cs");
            var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
            var fileNamespace = EmitterUtilities.BuildNamespace(target, feature.RelativeDirectory);
            var contractNamespace = EmitterUtilities.ResolveImportNamespace(target, "contracts");
            var uiEngineNamespace = EmitterUtilities.ResolveImportNamespace(target, "uiEngines");
            var initVm = feature.Operations.FirstOrDefault(static operation => string.Equals(operation.Name, "InitVm", StringComparison.Ordinal));

            var builder = new CodeBuilder();
            EmitterUtilities.AppendHeader(builder, target.Name, Kind, relativePath);
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Threading;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using Microsoft.AspNetCore.Mvc;");
            builder.AppendLine("using Microsoft.AspNetCore.Mvc.RazorPages;");
            if (!string.Equals(contractNamespace, fileNamespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {contractNamespace};");
            }

            if (!string.Equals(uiEngineNamespace, fileNamespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {uiEngineNamespace};");
            }

            builder.AppendLine();
            builder.AppendLine($"namespace {fileNamespace};");
            builder.AppendLine();
            builder.AppendLine($"public abstract partial class {feature.Name}PageModelBase : PageModel");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                builder.AppendLine($"protected {feature.Name}PageModelBase(I{feature.Name}UiEngine uiEngine)");
                builder.AppendLine("{");
                using (builder.Indent())
                {
                    builder.AppendLine("ArgumentNullException.ThrowIfNull(uiEngine);");
                    builder.AppendLine("UiEngine = uiEngine;");
                }

                builder.AppendLine("}");
                builder.AppendLine();
                builder.AppendLine($"protected I{feature.Name}UiEngine UiEngine {{ get; }}");

                if (feature.ViewModelProperties.Count > 0 || initVm is not null)
                {
                    builder.AppendLine();
                    builder.AppendLine($"public {feature.ViewModelTypeName}? ViewModel {{ get; protected set; }}");
                }

                foreach (var pageParameter in feature.PageParameters)
                {
                    builder.AppendLine();
                    var supportsGet = pageParameter.Source is PageParameterSource.Query or PageParameterSource.Route;
                    var propertyName = EmitterUtilities.ToPascalCase(pageParameter.Name);
                    builder.AppendLine(supportsGet ? "[BindProperty(SupportsGet = true)]" : "[BindProperty]");
                    var typeName = EmitterUtilities.FormatTypeName(pageParameter.Type);
                    var defaultInitializer = TypeNameClassifier.IsValueType(typeName.TrimEnd('?')) || typeName.EndsWith("?", StringComparison.Ordinal)
                        ? string.Empty
                        : " = default!;";
                    builder.AppendLine($"public {typeName} {propertyName} {{ get; set; }}{defaultInitializer}");
                }

                if (initVm is not null)
                {
                    builder.AppendLine();
                    builder.AppendLine("public virtual async Task OnGetAsync(CancellationToken cancellationToken)");
                    builder.AppendLine("{");
                    using (builder.Indent())
                    {
                        var arguments = string.Join(", ", feature.PageParameters.Select(static parameter => EmitterUtilities.ToPascalCase(parameter.Name)).Concat(["cancellationToken"]));
                        builder.AppendLine($"ViewModel = await UiEngine.{EmitterUtilities.GetOperationMethodName(initVm)}({arguments});");
                    }

                    builder.AppendLine("}");
                }

                foreach (var operation in feature.Operations.Where(static operation => !string.Equals(operation.Name, "InitVm", StringComparison.Ordinal)))
                {
                    builder.AppendLine();
                    var resultType = EmitterUtilities.GetOperationResultType(feature, operation);
                    var returnType = resultType is null ? "Task" : $"Task<{resultType}>";
                    var parameters = EmitterUtilities.BuildAdapterMethodParameters(operation);
                    builder.AppendLine($"protected {returnType} Execute{operation.Name}Async({EmitterUtilities.FormatMethodSignature(parameters, includeCancellationToken: true)})");
                    builder.AppendLine("{");
                    using (builder.Indent())
                    {
                        builder.AppendLine($"return UiEngine.{EmitterUtilities.GetOperationMethodName(operation)}({EmitterUtilities.FormatAdapterInvocationArguments(feature, operation)});");
                    }

                    builder.AppendLine("}");
                }
            }

            builder.AppendLine("}");

            generatedFiles.Add(new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString()));
        }

        return generatedFiles;
    }
}
