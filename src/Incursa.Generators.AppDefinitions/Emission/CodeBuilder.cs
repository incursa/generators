namespace Incursa.Generators.AppDefinitions.Emission;

using System.Text;

internal sealed class CodeBuilder
{
    private readonly StringBuilder builder = new();
    private int indentLevel;

    public void AppendLine(string line = "")
    {
        if (line.Length == 0)
        {
            builder.Append('\n');
            return;
        }

        builder.Append(' ', indentLevel * 4);
        builder.Append(line);
        builder.Append('\n');
    }

    public IDisposable Indent()
    {
        indentLevel++;
        return new Indentation(this);
    }

    public override string ToString()
    {
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class Indentation(CodeBuilder owner) : IDisposable
    {
        public void Dispose()
        {
            owner.indentLevel--;
        }
    }
}
