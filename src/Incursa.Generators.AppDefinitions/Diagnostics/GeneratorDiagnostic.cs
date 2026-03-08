namespace Incursa.Generators.AppDefinitions.Diagnostics;

public sealed record GeneratorDiagnostic(
    string Code,
    GeneratorDiagnosticSeverity Severity,
    string Message,
    SourceLocation? Location = null)
{
    public override string ToString()
    {
        if (Location is null)
        {
            return $"{Severity} {Code}: {Message}";
        }

        return $"{Location}: {Severity} {Code}: {Message}";
    }
}
