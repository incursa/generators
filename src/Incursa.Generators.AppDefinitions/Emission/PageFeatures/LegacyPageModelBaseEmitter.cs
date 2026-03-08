namespace Incursa.Generators.AppDefinitions.Emission.PageFeatures;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public sealed class LegacyPageModelBaseEmitter : IGenerationTargetEmitter
{
    public string Kind => "page-model-base-legacy";

    public IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics)
    {
        var generatedFiles = new List<GeneratedFile>();
        var uiEngineNamespace = EmitterUtilities.ResolveImportNamespace(target, "uiEngines");
        var contractNamespace = EmitterUtilities.ResolveImportNamespace(target, "contracts");

        foreach (var feature in model.PageFeatures.Where(filter.IsMatch).OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            var relativePath = EmitterUtilities.BuildRelativePath(target, feature.RelativeDirectory, $"{feature.Name}ModelBase.g.cs");
            var absolutePath = EmitterUtilities.BuildAbsolutePath(target, relativePath);
            var builder = new CodeBuilder();

            LegacyPageFeatureUtilities.AppendLegacySourceHeader(builder, model, feature, enableNullable: false);
            builder.AppendLine($"namespace {target.Namespace};");
            builder.AppendLine();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using System.Threading;");
            builder.AppendLine("using CommunityToolkit.Diagnostics;");
            builder.AppendLine("using Microsoft.AspNetCore.Mvc;");
            builder.AppendLine("using Microsoft.AspNetCore.Mvc.RazorPages;");
            builder.AppendLine("using Sentry;");
            builder.AppendLine("using PayeWaive;");
            if (!string.Equals(uiEngineNamespace, target.Namespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {uiEngineNamespace};");
            }

            if (!string.Equals(contractNamespace, target.Namespace, StringComparison.Ordinal))
            {
                builder.AppendLine($"using {contractNamespace};");
            }

            builder.AppendLine();
            builder.AppendLine($"public abstract class {feature.Name}ModelBase : PageModel");
            builder.AppendLine("{");
            using (builder.Indent())
            {
                var scopeType = LegacyPageFeatureUtilities.GetLegacyScopeType(feature);
                var contextType = LegacyPageFeatureUtilities.GetLegacyContextType(feature);
                var requestScopeAccessor = LegacyPageFeatureUtilities.GetLegacyRequestScopeAccessor(feature);

                builder.AppendLine("public Sentry.IHub Hub { get; private set; }");
                builder.AppendLine();
                builder.AppendLine($"public I{feature.Name}UiEngine Engine {{ get; private set; }}");
                builder.AppendLine();
                builder.AppendLine("public PwRequestInfo PwRequestInfo { get; private set; }");
                builder.AppendLine();
                builder.AppendLine($"public {scopeType} Scope => this.PwRequestInfo.{requestScopeAccessor};");
                builder.AppendLine();
                builder.AppendLine($"public {contextType} Context => this.PwRequestInfo.{contextType};");
                builder.AppendLine();
                builder.AppendLine("public UserObjectId? SubjectId => this.PwRequestInfo.SubjectId;");
                builder.AppendLine();
                builder.AppendLine("public string SecuritySubjectId => this.PwRequestInfo.SecuritySubjectId;");
                builder.AppendLine();
                builder.AppendLine("[MaybeNull]");
                builder.AppendLine("public string ImpersonatedSubjectId => this.PwRequestInfo.ImpersonatedSubjectId;");
                builder.AppendLine();
                builder.AppendLine("public ILogger Logger => this.PwRequestInfo.Logger;");
                builder.AppendLine();
                builder.AppendLine("public bool IsPwAdmin => this.PwRequestInfo.IsPwAdmin;");
                builder.AppendLine();
                builder.AppendLine("public bool IsImpersonating => this.PwRequestInfo.IsImpersonating;");
                builder.AppendLine();
                builder.AppendLine("public string? UserEmail => this.PwRequestInfo.UserEmail;");
                builder.AppendLine();
                builder.AppendLine("public string? UserName => this.PwRequestInfo.UserName;");
                builder.AppendLine();
                builder.AppendLine($"public {feature.Name}ModelBase(I{feature.Name}UiEngine uiEngine, Sentry.IHub sentryHub, PwRequestInfo pwRequestInfo)");
                builder.AppendLine("{");
                using (builder.Indent())
                {
                    builder.AppendLine("Guard.IsNotNull(uiEngine);");
                    builder.AppendLine("Guard.IsNotNull(sentryHub);");
                    builder.AppendLine("Guard.IsNotNull(pwRequestInfo);");
                    builder.AppendLine("this.Engine = uiEngine;");
                    builder.AppendLine("this.Hub = sentryHub;");
                    builder.AppendLine("this.PwRequestInfo = pwRequestInfo;");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("}");
            generatedFiles.Add(new GeneratedFile(target.Name, Kind, absolutePath, relativePath, builder.ToString()));
        }

        return generatedFiles;
    }
}
