namespace Incursa.Generators.AppDefinitions.Diagnostics;

public sealed class DiagnosticBag
{
    private readonly List<GeneratorDiagnostic> diagnostics = [];

    public IReadOnlyList<GeneratorDiagnostic> Items => diagnostics;

    public bool HasErrors => diagnostics.Any(static diagnostic => diagnostic.Severity == GeneratorDiagnosticSeverity.Error);

    public int ErrorCount => diagnostics.Count(static diagnostic => diagnostic.Severity == GeneratorDiagnosticSeverity.Error);

    public void Add(GeneratorDiagnostic diagnostic)
    {
        diagnostics.Add(diagnostic);
    }

    public void AddError(string code, string message, SourceLocation? location = null)
    {
        diagnostics.Add(new GeneratorDiagnostic(code, GeneratorDiagnosticSeverity.Error, message, location));
    }

    public void AddWarning(string code, string message, SourceLocation? location = null)
    {
        diagnostics.Add(new GeneratorDiagnostic(code, GeneratorDiagnosticSeverity.Warning, message, location));
    }

    public void AddInfo(string code, string message, SourceLocation? location = null)
    {
        diagnostics.Add(new GeneratorDiagnostic(code, GeneratorDiagnosticSeverity.Info, message, location));
    }
}
