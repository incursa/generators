namespace Incursa.Generators.AppDefinitions.Diagnostics;

public sealed record SourceLocation(string FilePath, int? Line = null, int? Column = null)
{
    public static SourceLocation FromFile(string filePath) => new(Path.GetFullPath(filePath));

    public override string ToString()
    {
        if (Line is null)
        {
            return FilePath;
        }

        if (Column is null)
        {
            return $"{FilePath}({Line})";
        }

        return $"{FilePath}({Line},{Column})";
    }
}
