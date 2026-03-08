namespace Incursa.Generators.AppDefinitions.Pipeline;

using System.Text.RegularExpressions;
using Incursa.Generators.AppDefinitions.Model;

public sealed class FeatureFilter
{
    private readonly Regex? pattern;

    private FeatureFilter(string? rawPattern)
    {
        RawPattern = rawPattern;
        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            return;
        }

        var regexPattern = "^" + Regex.Escape(rawPattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        pattern = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public string? RawPattern { get; }

    public static FeatureFilter All { get; } = new(null);

    public static FeatureFilter Create(string? rawPattern) => new(rawPattern);

    public bool IsMatch(PageFeatureDefinition feature)
    {
        if (pattern is null)
        {
            return true;
        }

        return pattern.IsMatch(feature.Name)
            || pattern.IsMatch(feature.RelativeDirectory.Replace(Path.DirectorySeparatorChar, '/'));
    }
}
