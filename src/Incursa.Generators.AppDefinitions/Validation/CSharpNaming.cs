namespace Incursa.Generators.AppDefinitions.Validation;

using System.Text;
using System.Text.RegularExpressions;

public static partial class CSharpNaming
{
    private static readonly Regex IdentifierPattern = CreateIdentifierPattern();

    public static bool IsValidIdentifier(string value)
    {
        return IdentifierPattern.IsMatch(value);
    }

    public static string SanitizeNamespaceSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var isValid = index == 0
                ? char.IsLetter(character) || character == '_'
                : char.IsLetterOrDigit(character) || character == '_';

            builder.Append(isValid ? character : '_');
        }

        var sanitized = builder.ToString();
        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateIdentifierPattern();
}
