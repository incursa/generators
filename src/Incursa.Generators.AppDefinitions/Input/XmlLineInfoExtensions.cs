namespace Incursa.Generators.AppDefinitions.Input;

using System.Xml;
using System.Xml.Linq;
using Incursa.Generators.AppDefinitions.Diagnostics;

internal static class XmlLineInfoExtensions
{
    public static SourceLocation GetLocation(this XObject node, string filePath)
    {
        if (node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return new SourceLocation(Path.GetFullPath(filePath), lineInfo.LineNumber, lineInfo.LinePosition);
        }

        return SourceLocation.FromFile(filePath);
    }
}
