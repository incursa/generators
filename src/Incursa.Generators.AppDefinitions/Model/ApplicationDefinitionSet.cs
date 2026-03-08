namespace Incursa.Generators.AppDefinitions.Model;

using Incursa.Generators.AppDefinitions.Diagnostics;

public sealed record ApplicationDefinitionSet(
    string DefinitionRootPath,
    IReadOnlyList<PageFeatureDefinition> PageFeatures);

public sealed record PageFeatureDefinition(
    string Name,
    string SourceFilePath,
    string RelativeDirectory,
    SourceLocation Location,
    string? Description,
    string? Route,
    string? ScopeHandling,
    bool AllowAnonymous,
    bool Navigable,
    IReadOnlyList<PageParameterDefinition> PageParameters,
    IReadOnlyList<PropertyDefinition> ViewModelProperties,
    IReadOnlyList<ComplexTypeDefinition> OwnedTypes,
    IReadOnlyList<ComplexTypeDefinition> ApiModels,
    IReadOnlyList<OperationDefinition> Operations)
{
    public string ViewModelTypeName => $"{Name}ViewModel";
}

public enum PageParameterSource
{
    Route,
    Query,
    Body,
    Form,
    Header,
}

public enum ComplexTypeKind
{
    OwnedViewModel,
    ApiModel,
}

public sealed record PageParameterDefinition(
    string Name,
    string Type,
    PageParameterSource Source,
    bool Required,
    SourceLocation Location);

public sealed record PropertyDefinition(
    string Name,
    string Type,
    SourceLocation Location,
    bool Required,
    bool Nullable,
    bool Settable,
    string? JsonName,
    string? DefaultValue,
    string? Regex,
    string? Expression,
    decimal? Min,
    decimal? Max,
    bool NoDefault);

public sealed record ComplexTypeDefinition(
    ComplexTypeKind Kind,
    string Name,
    string? Inherits,
    SourceLocation Location,
    IReadOnlyList<PropertyDefinition> Properties);

public sealed record OperationDefinition(
    string Name,
    SourceLocation Location,
    string? HttpMethod,
    string? ApiRouteSegment,
    string? ReturnType,
    IReadOnlyList<OperationParameterDefinition> Parameters,
    IReadOnlyList<OperationParameterDefinition> RouteParameters,
    IReadOnlyList<OperationParameterDefinition> QueryParameters,
    BodyParameterDefinition? BodyParameter,
    ReturnDefinition? Returns);

public sealed record OperationParameterDefinition(
    string Name,
    string Type,
    bool Required,
    SourceLocation Location);

public sealed record BodyParameterDefinition(
    string Type,
    SourceLocation Location);

public sealed record ReturnDefinition(
    string Type,
    string? SpecialHandling,
    SourceLocation Location);
