namespace Incursa.Generators.AppDefinitions.Validation;

using System.Text.RegularExpressions;

public static partial class TypeNameClassifier
{
    private static readonly HashSet<string> BuiltInTypes =
    [
        "bool",
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "uint",
        "long",
        "ulong",
        "nint",
        "nuint",
        "float",
        "double",
        "decimal",
        "char",
        "string",
        "object",
        "DateOnly",
        "DateTime",
        "DateTimeOffset",
        "Dictionary",
        "Guid",
        "IAsyncEnumerable",
        "ICollection",
        "IDictionary",
        "IEnumerable",
        "IList",
        "IReadOnlyCollection",
        "IReadOnlyDictionary",
        "IReadOnlyList",
        "KeyValuePair",
        "List",
        "Nullable",
        "ReadOnlyCollection",
        "Task",
        "TimeOnly",
        "TimeSpan",
        "ValueTask",
        "CancellationToken",
    ];

    private static readonly HashSet<string> ValueTypeAliases =
    [
        "bool",
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "uint",
        "long",
        "ulong",
        "nint",
        "nuint",
        "float",
        "double",
        "decimal",
        "char",
        "DateOnly",
        "DateTime",
        "DateTimeOffset",
        "Guid",
        "TimeOnly",
        "TimeSpan",
    ];

    public static IReadOnlyList<string> EnumerateNamedTypes(string typeExpression)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
        {
            return [];
        }

        return TypeTokenPattern()
            .Matches(typeExpression)
            .Select(static match => match.Value.Replace("global::", string.Empty, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsBuiltIn(string typeName) => BuiltInTypes.Contains(typeName);

    public static bool IsValueType(string typeName)
    {
        var normalized = typeName.TrimEnd('?');
        return ValueTypeAliases.Contains(normalized);
    }

    public static bool LooksFullyQualified(string typeName)
    {
        return typeName.Contains('.', StringComparison.Ordinal);
    }

    [GeneratedRegex("(?:global::)?[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*", RegexOptions.CultureInvariant)]
    private static partial Regex TypeTokenPattern();
}
