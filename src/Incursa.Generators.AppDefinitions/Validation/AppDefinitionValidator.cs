namespace Incursa.Generators.AppDefinitions.Validation;

using System.Text.RegularExpressions;
using Incursa.Generators.AppDefinitions.Config;
using Incursa.Generators.AppDefinitions.Diagnostics;
using Incursa.Generators.AppDefinitions.Model;

public sealed partial class AppDefinitionValidator
{
    public void Validate(ResolvedGeneratorConfig config, ApplicationDefinitionSet model, DiagnosticBag diagnostics)
    {
        var featuresByName = new Dictionary<string, PageFeatureDefinition>(StringComparer.Ordinal);

        foreach (var feature in model.PageFeatures.OrderBy(static feature => feature.Name, StringComparer.Ordinal))
        {
            ValidateFeature(feature, config.Validation, diagnostics);

            if (!featuresByName.TryAdd(feature.Name, feature))
            {
                diagnostics.AddError(
                    "APPDEF020",
                    $"Duplicate feature name '{feature.Name}'. Feature names must be unique across all definition files.",
                    feature.Location);
            }
        }
    }

    private static void ValidateFeature(PageFeatureDefinition feature, ValidationSettings settings, DiagnosticBag diagnostics)
    {
        ValidateIdentifier(feature.Name, "feature", feature.Location, diagnostics);

        ValidateDuplicateNames(feature.PageParameters.Select(static parameter => (parameter.Name, parameter.Location)), "page parameter", diagnostics);
        ValidateDuplicateNames(feature.ViewModelProperties.Select(static property => (property.Name, property.Location)), "view-model property", diagnostics);
        ValidateDuplicateNames(feature.Operations.Select(static operation => (operation.Name, operation.Location)), "operation", diagnostics);

        var localTypes = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);
        localTypes.Add(feature.ViewModelTypeName, feature.Location);

        foreach (var type in feature.OwnedTypes.Concat(feature.ApiModels).OrderBy(static type => type.Name, StringComparer.Ordinal))
        {
            ValidateIdentifier(type.Name, "type", type.Location, diagnostics);
            if (!localTypes.TryAdd(type.Name, type.Location))
            {
                diagnostics.AddError("APPDEF021", $"Duplicate local type name '{type.Name}' in feature '{feature.Name}'.", type.Location);
            }

            ValidateDuplicateNames(type.Properties.Select(static property => (property.Name, property.Location)), $"property on type '{type.Name}'", diagnostics);
            foreach (var property in type.Properties)
            {
                ValidateProperty(property, settings, localTypes.Keys, diagnostics);
            }

            if (!string.IsNullOrWhiteSpace(type.Inherits))
            {
                ValidateTypeExpression(type.Inherits, type.Location, settings, localTypes.Keys, diagnostics);
            }
        }

        foreach (var pageParameter in feature.PageParameters)
        {
            ValidateIdentifier(pageParameter.Name, "page parameter", pageParameter.Location, diagnostics);
            ValidateTypeExpression(pageParameter.Type, pageParameter.Location, settings, localTypes.Keys, diagnostics);
        }

        foreach (var property in feature.ViewModelProperties)
        {
            ValidateProperty(property, settings, localTypes.Keys, diagnostics);
        }

        foreach (var operation in feature.Operations)
        {
            ValidateOperation(feature, operation, settings, localTypes.Keys, diagnostics);
        }

        var initVm = feature.Operations.FirstOrDefault(static operation => string.Equals(operation.Name, "InitVm", StringComparison.Ordinal));
        if (feature.ViewModelProperties.Count > 0 && initVm is null)
        {
            diagnostics.AddWarning(
                "APPDEF022",
                $"Feature '{feature.Name}' defines view-model properties but does not declare an InitVm operation. Generated page model bases will not include OnGetAsync initialization.",
                feature.Location);
        }
    }

    private static void ValidateOperation(
        PageFeatureDefinition feature,
        OperationDefinition operation,
        ValidationSettings settings,
        IEnumerable<string> localTypeNames,
        DiagnosticBag diagnostics)
    {
        ValidateIdentifier(operation.Name, "operation", operation.Location, diagnostics);
        ValidateDuplicateNames(operation.Parameters.Select(static parameter => (parameter.Name, parameter.Location)), $"parameter on operation '{operation.Name}'", diagnostics);
        ValidateDuplicateNames(operation.RouteParameters.Select(static parameter => (parameter.Name, parameter.Location)), $"route parameter on operation '{operation.Name}'", diagnostics);
        ValidateDuplicateNames(operation.QueryParameters.Select(static parameter => (parameter.Name, parameter.Location)), $"query parameter on operation '{operation.Name}'", diagnostics);

        var methodScopeNames = new HashSet<string>(feature.PageParameters.Select(static parameter => parameter.Name), StringComparer.Ordinal);
        foreach (var parameter in operation.Parameters.Concat(operation.RouteParameters).Concat(operation.QueryParameters))
        {
            ValidateIdentifier(parameter.Name, "operation parameter", parameter.Location, diagnostics);
            ValidateTypeExpression(parameter.Type, parameter.Location, settings, localTypeNames, diagnostics);

            if (!methodScopeNames.Add(parameter.Name))
            {
                diagnostics.AddError(
                    "APPDEF023",
                    $"Operation '{operation.Name}' cannot reuse parameter name '{parameter.Name}' because generated adapter signatures would collide.",
                    parameter.Location);
            }
        }

        if (operation.BodyParameter is not null)
        {
            ValidateTypeExpression(operation.BodyParameter.Type, operation.BodyParameter.Location, settings, localTypeNames, diagnostics);
            if (!methodScopeNames.Add("body"))
            {
                diagnostics.AddError("APPDEF024", $"Operation '{operation.Name}' body parameter collides with an existing parameter name.", operation.BodyParameter.Location);
            }
        }

        if (operation.Returns is not null)
        {
            ValidateTypeExpression(operation.Returns.Type, operation.Returns.Location, settings, localTypeNames, diagnostics);
        }

        if (!string.IsNullOrWhiteSpace(operation.ReturnType))
        {
            ValidateTypeExpression(operation.ReturnType, operation.Location, settings, localTypeNames, diagnostics);
        }

        if (string.Equals(operation.Name, "InitVm", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(operation.ReturnType)
            && !string.Equals(operation.ReturnType, feature.ViewModelTypeName, StringComparison.Ordinal))
        {
            diagnostics.AddError(
                "APPDEF025",
                $"InitVm operation on feature '{feature.Name}' must return '{feature.ViewModelTypeName}' when an explicit return type is declared.",
                operation.Location);
        }

        ValidateRouteParameterConsistency(operation, diagnostics);
    }

    private static void ValidateProperty(
        PropertyDefinition property,
        ValidationSettings settings,
        IEnumerable<string> localTypeNames,
        DiagnosticBag diagnostics)
    {
        ValidateIdentifier(property.Name, "property", property.Location, diagnostics);
        ValidateTypeExpression(property.Type, property.Location, settings, localTypeNames, diagnostics);
    }

    private static void ValidateTypeExpression(
        string typeExpression,
        SourceLocation location,
        ValidationSettings settings,
        IEnumerable<string> localTypeNames,
        DiagnosticBag diagnostics)
    {
        var localTypeSet = localTypeNames.ToHashSet(StringComparer.Ordinal);
        var knownTypes = settings.KnownTypeNames.ToHashSet(StringComparer.Ordinal);

        foreach (var token in TypeNameClassifier.EnumerateNamedTypes(typeExpression))
        {
            if (TypeNameClassifier.IsBuiltIn(token)
                || knownTypes.Contains(token)
                || localTypeSet.Contains(token)
                || TypeNameClassifier.LooksFullyQualified(token))
            {
                continue;
            }

            if (settings.AllowUnqualifiedExternalTypes)
            {
                continue;
            }

            diagnostics.AddError(
                "APPDEF026",
                $"Type reference '{token}' in '{typeExpression}' could not be resolved. Use a local type name, a configured known type name, or a fully qualified external type name.",
                location);
        }
    }

    private static void ValidateIdentifier(string value, string subject, SourceLocation location, DiagnosticBag diagnostics)
    {
        if (!CSharpNaming.IsValidIdentifier(value))
        {
            diagnostics.AddError("APPDEF027", $"The {subject} name '{value}' is not a valid C# identifier.", location);
        }
    }

    private static void ValidateDuplicateNames(IEnumerable<(string Name, SourceLocation Location)> values, string subject, DiagnosticBag diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!seen.Add(value.Name))
            {
                diagnostics.AddError("APPDEF028", $"Duplicate {subject} name '{value.Name}'.", value.Location);
            }
        }
    }

    private static void ValidateRouteParameterConsistency(OperationDefinition operation, DiagnosticBag diagnostics)
    {
        var namesInRouteSegment = RouteParameterPattern()
            .Matches(operation.ApiRouteSegment ?? string.Empty)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var definedRouteNames = operation.RouteParameters
            .Select(static parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var missingName in definedRouteNames.Except(namesInRouteSegment, StringComparer.Ordinal))
        {
            diagnostics.AddError(
                "APPDEF029",
                $"Operation '{operation.Name}' defines route parameter '{missingName}' but its route segment does not contain '{{{missingName}}}'.",
                operation.Location);
        }

        foreach (var missingName in namesInRouteSegment.Except(definedRouteNames, StringComparer.Ordinal))
        {
            diagnostics.AddError(
                "APPDEF030",
                $"Operation '{operation.Name}' route segment contains '{{{missingName}}}' but no matching RouteParameter is declared.",
                operation.Location);
        }
    }

    [GeneratedRegex("\\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteParameterPattern();
}
