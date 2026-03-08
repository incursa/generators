namespace Incursa.Generators.AppDefinitions.Input;

using System.Globalization;
using System.Xml.Linq;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;

public sealed class AppDefinitionParser
{
    public ApplicationDefinitionSet Parse(string definitionRoot, IReadOnlyList<string> patterns, DiagnosticBag diagnostics)
    {
        var fullDefinitionRoot = Path.GetFullPath(definitionRoot);
        if (!Directory.Exists(fullDefinitionRoot))
        {
            diagnostics.AddError("APPDEF010", $"Definition root '{fullDefinitionRoot}' does not exist.", SourceLocation.FromFile(fullDefinitionRoot));
            return new ApplicationDefinitionSet(fullDefinitionRoot, []);
        }

        var files = patterns
            .SelectMany(pattern => Directory.EnumerateFiles(fullDefinitionRoot, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pageFeatures = new List<PageFeatureDefinition>();
        foreach (var file in files)
        {
            var feature = ParseFile(fullDefinitionRoot, file, diagnostics);
            if (feature is not null)
            {
                pageFeatures.Add(feature);
            }
        }

        return new ApplicationDefinitionSet(fullDefinitionRoot, pageFeatures);
    }

    private PageFeatureDefinition? ParseFile(string definitionRoot, string filePath, DiagnosticBag diagnostics)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(filePath, LoadOptions.SetLineInfo);
        }
        catch (Exception exception)
        {
            diagnostics.AddError("APPDEF011", $"Failed to parse XML definition '{filePath}'. {exception.Message}", SourceLocation.FromFile(filePath));
            return null;
        }

        var root = document.Root;
        if (root is null)
        {
            diagnostics.AddError("APPDEF012", $"Definition file '{filePath}' is empty.", SourceLocation.FromFile(filePath));
            return null;
        }

        if (!IsSupportedRootElement(root.Name.LocalName))
        {
            diagnostics.AddError("APPDEF013", $"Definition file '{filePath}' has unsupported root element '{root.Name.LocalName}'.", root.GetLocation(filePath));
            return null;
        }

        var name = GetRequiredAttribute(root, "name", filePath, diagnostics);
        if (name is null)
        {
            return null;
        }

        var relativeDirectory = GetRelativeDirectory(definitionRoot, filePath);
        var description = GetOptionalElementValue(root, "Description");

        var pageParameters = GetOptionalChild(root, "PageParameters")?
            .Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "Parameter", StringComparison.Ordinal))
            .Select(parameter => ParsePageParameter(parameter, filePath, diagnostics))
            .Where(static parameter => parameter is not null)
            .Cast<PageParameterDefinition>()
            .ToArray() ?? [];

        var viewModelProperties = GetOptionalChild(root, "ViewModelProperties")?
            .Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "Property", StringComparison.Ordinal))
            .Select(property => ParseProperty(property, filePath, diagnostics))
            .Where(static property => property is not null)
            .Cast<PropertyDefinition>()
            .ToArray() ?? [];

        var ownedTypes = new List<ComplexTypeDefinition>();
        foreach (var element in root.Elements().Where(static element => string.Equals(element.Name.LocalName, "ViewModelOwnedType", StringComparison.Ordinal)))
        {
            var ownedType = ParseComplexType(element, ComplexTypeKind.OwnedViewModel, filePath, diagnostics);
            if (ownedType is not null)
            {
                ownedTypes.Add(ownedType);
            }
        }

        var ownedTypesContainer = GetOptionalChild(root, "OwnedTypes");
        if (ownedTypesContainer is not null)
        {
            foreach (var element in ownedTypesContainer.Elements().Where(static element => string.Equals(element.Name.LocalName, "Type", StringComparison.Ordinal)))
            {
                var ownedType = ParseComplexType(element, ComplexTypeKind.OwnedViewModel, filePath, diagnostics);
                if (ownedType is not null)
                {
                    ownedTypes.Add(ownedType);
                }
            }
        }

        var apiModels = new List<ComplexTypeDefinition>();
        foreach (var element in root.Elements().Where(static element => string.Equals(element.Name.LocalName, "ApiModel", StringComparison.Ordinal)))
        {
            var apiModel = ParseComplexType(element, ComplexTypeKind.ApiModel, filePath, diagnostics);
            if (apiModel is not null)
            {
                apiModels.Add(apiModel);
            }
        }

        var apiModelsContainer = GetOptionalChild(root, "ApiModels");
        if (apiModelsContainer is not null)
        {
            foreach (var element in apiModelsContainer.Elements().Where(static element => string.Equals(element.Name.LocalName, "Type", StringComparison.Ordinal)))
            {
                var apiModel = ParseComplexType(element, ComplexTypeKind.ApiModel, filePath, diagnostics);
                if (apiModel is not null)
                {
                    apiModels.Add(apiModel);
                }
            }
        }

        var operations = GetOptionalChild(root, "Operations")?
            .Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "Operation", StringComparison.Ordinal))
            .Select(operation => ParseOperation(operation, filePath, diagnostics))
            .Where(static operation => operation is not null)
            .Cast<OperationDefinition>()
            .ToArray() ?? [];

        return new PageFeatureDefinition(
            name,
            Path.GetFullPath(filePath),
            relativeDirectory,
            root.GetLocation(filePath),
            description,
            GetOptionalAttribute(root, "route"),
            GetOptionalAttribute(root, "scopeHandling") ?? GetOptionalAttribute(root, "scope"),
            GetOptionalBoolAttribute(root, "allowAnonymous", false, filePath, diagnostics),
            GetOptionalBoolAttribute(root, "navigable", true, filePath, diagnostics),
            pageParameters,
            viewModelProperties,
            ownedTypes.OrderBy(static type => type.Name, StringComparer.Ordinal).ToArray(),
            apiModels.OrderBy(static type => type.Name, StringComparer.Ordinal).ToArray(),
            operations.OrderBy(static operation => operation.Name, StringComparer.Ordinal).ToArray());
    }

    private static bool IsSupportedRootElement(string localName)
    {
        return string.Equals(localName, "PageFeature", StringComparison.Ordinal)
            || string.Equals(localName, "PageDefinition", StringComparison.Ordinal)
            || string.Equals(localName, "ComponentDefinition", StringComparison.Ordinal);
    }

    private static PageParameterDefinition? ParsePageParameter(XElement element, string filePath, DiagnosticBag diagnostics)
    {
        var name = GetRequiredAttribute(element, "name", filePath, diagnostics);
        var type = GetRequiredAttribute(element, "type", filePath, diagnostics);
        var sourceValue = GetRequiredAttribute(element, "source", filePath, diagnostics);

        if (name is null || type is null || sourceValue is null)
        {
            return null;
        }

        if (!Enum.TryParse<PageParameterSource>(sourceValue, ignoreCase: true, out var source))
        {
            diagnostics.AddError("APPDEF014", $"Unsupported page parameter source '{sourceValue}'.", element.GetLocation(filePath));
            return null;
        }

        return new PageParameterDefinition(
            name,
            type,
            source,
            GetOptionalBoolAttribute(element, "required", false, filePath, diagnostics),
            element.GetLocation(filePath));
    }

    private static PropertyDefinition? ParseProperty(XElement element, string filePath, DiagnosticBag diagnostics)
    {
        var name = GetRequiredAttribute(element, "name", filePath, diagnostics);
        var type = GetRequiredAttribute(element, "type", filePath, diagnostics);

        if (name is null || type is null)
        {
            return null;
        }

        return new PropertyDefinition(
            name,
            type,
            element.GetLocation(filePath),
            GetOptionalBoolAttribute(element, "required", false, filePath, diagnostics),
            GetOptionalBoolAttribute(element, "nullable", false, filePath, diagnostics),
            GetOptionalBoolAttribute(element, "settable", true, filePath, diagnostics),
            GetOptionalAttribute(element, "json-property") ?? GetOptionalAttribute(element, "jsonProperty"),
            GetOptionalAttribute(element, "defaultValue"),
            GetOptionalAttribute(element, "regex"),
            GetOptionalAttribute(element, "expression"),
            GetOptionalDecimalAttribute(element, "min", filePath, diagnostics),
            GetOptionalDecimalAttribute(element, "max", filePath, diagnostics),
            GetOptionalBoolAttribute(element, "noDefault", false, filePath, diagnostics));
    }

    private static ComplexTypeDefinition? ParseComplexType(XElement element, ComplexTypeKind kind, string filePath, DiagnosticBag diagnostics)
    {
        var name = GetRequiredAttribute(element, "name", filePath, diagnostics);
        if (name is null)
        {
            return null;
        }

        var properties = element.Elements()
            .Where(static child => string.Equals(child.Name.LocalName, "Property", StringComparison.Ordinal))
            .Select(property => ParseProperty(property, filePath, diagnostics))
            .Where(static property => property is not null)
            .Cast<PropertyDefinition>()
            .ToArray();

        return new ComplexTypeDefinition(
            kind,
            name,
            GetOptionalAttribute(element, "inherits"),
            element.GetLocation(filePath),
            properties);
    }

    private static OperationDefinition? ParseOperation(XElement element, string filePath, DiagnosticBag diagnostics)
    {
        var name = GetRequiredAttribute(element, "name", filePath, diagnostics);
        if (name is null)
        {
            return null;
        }

        var parameters = new List<OperationParameterDefinition>();
        var routeParameters = new List<OperationParameterDefinition>();
        var queryParameters = new List<OperationParameterDefinition>();
        BodyParameterDefinition? bodyParameter = null;
        ReturnDefinition? returns = null;

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Parameter":
                    {
                        var parameter = ParseOperationParameter(child, filePath, diagnostics);
                        if (parameter is not null)
                        {
                            parameters.Add(parameter);
                        }

                        break;
                    }

                case "RouteParameter":
                    {
                        var routeParameter = ParseOperationParameter(child, filePath, diagnostics);
                        if (routeParameter is not null)
                        {
                            routeParameters.Add(routeParameter);
                        }

                        break;
                    }

                case "QueryParameter":
                    {
                        var queryParameter = ParseOperationParameter(child, filePath, diagnostics);
                        if (queryParameter is not null)
                        {
                            queryParameters.Add(queryParameter);
                        }

                        break;
                    }

                case "BodyParameter":
                    {
                        var type = GetRequiredAttribute(child, "type", filePath, diagnostics);
                        if (type is not null)
                        {
                            bodyParameter = new BodyParameterDefinition(type, child.GetLocation(filePath));
                        }

                        break;
                    }

                case "Returns":
                    {
                        var type = GetRequiredAttribute(child, "type", filePath, diagnostics);
                        if (type is not null)
                        {
                            returns = new ReturnDefinition(type, GetOptionalAttribute(child, "specialHandling"), child.GetLocation(filePath));
                        }

                        break;
                    }
            }
        }

        var returnType = GetOptionalAttribute(element, "returnType") ?? returns?.Type;

        return new OperationDefinition(
            name,
            element.GetLocation(filePath),
            GetOptionalAttribute(element, "apiMethod") ?? GetOptionalAttribute(element, "httpMethod"),
            GetOptionalAttribute(element, "apiRouteSegment") ?? GetOptionalAttribute(element, "routeSegment"),
            returnType,
            parameters.OrderBy(static parameter => parameter.Name, StringComparer.Ordinal).ToArray(),
            routeParameters.OrderBy(static parameter => parameter.Name, StringComparer.Ordinal).ToArray(),
            queryParameters.OrderBy(static parameter => parameter.Name, StringComparer.Ordinal).ToArray(),
            bodyParameter,
            returns);
    }

    private static OperationParameterDefinition? ParseOperationParameter(XElement element, string filePath, DiagnosticBag diagnostics)
    {
        var name = GetRequiredAttribute(element, "name", filePath, diagnostics);
        var type = GetRequiredAttribute(element, "type", filePath, diagnostics);
        if (name is null || type is null)
        {
            return null;
        }

        return new OperationParameterDefinition(
            name,
            type,
            GetOptionalBoolAttribute(element, "required", false, filePath, diagnostics),
            element.GetLocation(filePath));
    }

    private static string? GetRequiredAttribute(XElement element, string attributeName, string filePath, DiagnosticBag diagnostics)
    {
        var value = GetOptionalAttribute(element, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.AddError("APPDEF015", $"Element '{element.Name.LocalName}' is missing required attribute '{attributeName}'.", element.GetLocation(filePath));
            return null;
        }

        return value;
    }

    private static string? GetOptionalAttribute(XElement element, string attributeName)
    {
        return element.Attributes().FirstOrDefault(
            attribute => string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string? GetOptionalElementValue(XElement element, string childName)
    {
        return GetOptionalChild(element, childName)?.Value.Trim();
    }

    private static XElement? GetOptionalChild(XElement element, string childName)
    {
        return element.Elements().FirstOrDefault(
            child => string.Equals(child.Name.LocalName, childName, StringComparison.Ordinal));
    }

    private static bool GetOptionalBoolAttribute(XElement element, string attributeName, bool defaultValue, string filePath, DiagnosticBag diagnostics)
    {
        var value = GetOptionalAttribute(element, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var parsedValue))
        {
            return parsedValue;
        }

        diagnostics.AddError("APPDEF016", $"Attribute '{attributeName}' on element '{element.Name.LocalName}' must be 'true' or 'false'.", element.GetLocation(filePath));
        return defaultValue;
    }

    private static decimal? GetOptionalDecimalAttribute(XElement element, string attributeName, string filePath, DiagnosticBag diagnostics)
    {
        var value = GetOptionalAttribute(element, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        diagnostics.AddError("APPDEF017", $"Attribute '{attributeName}' on element '{element.Name.LocalName}' must be a decimal number.", element.GetLocation(filePath));
        return null;
    }

    private static string GetRelativeDirectory(string definitionRoot, string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath) ?? definitionRoot;
        var relativePath = Path.GetRelativePath(definitionRoot, directoryPath);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
