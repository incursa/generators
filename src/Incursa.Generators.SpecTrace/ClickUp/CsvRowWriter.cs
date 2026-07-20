namespace Incursa.Generators.SpecTrace.ClickUp;

internal sealed class CsvRowWriter
{
    private readonly TextWriter writer;

    public CsvRowWriter(TextWriter writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void WriteRow(IEnumerable<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var first = true;
        foreach (var field in fields)
        {
            if (!first)
            {
                writer.Write(',');
            }

            WriteField(field ?? string.Empty);
            first = false;
        }

        writer.WriteLine();
    }

    private void WriteField(string value)
    {
        var mustQuote = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal);

        if (!mustQuote)
        {
            writer.Write(value);
            return;
        }

        writer.Write('"');
        foreach (var character in value)
        {
            if (character == '"')
            {
                writer.Write('"');
            }

            writer.Write(character);
        }

        writer.Write('"');
    }
}
