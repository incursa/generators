namespace Incursa.Generators.AppDefinitions.Emission;

using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;
using Incursa.Generators.AppDefinitions.Pipeline;

public interface IGenerationTargetEmitter
{
    string Kind { get; }

    IReadOnlyList<GeneratedFile> Emit(
        ResolvedOutputTarget target,
        ApplicationDefinitionSet model,
        FeatureFilter filter,
        DiagnosticBag diagnostics);
}
