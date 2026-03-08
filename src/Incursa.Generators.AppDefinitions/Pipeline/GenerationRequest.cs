namespace Incursa.Generators.AppDefinitions.Pipeline;

public sealed record GenerationRequest(
    string ConfigPath,
    string? DefinitionsPathOverride = null,
    string? FilterPattern = null);

public enum GenerationExecutionMode
{
    Validate,
    Write,
    Check,
}
