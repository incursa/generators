// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public static class DtoEntityGenerator
{
    public static string? Generate(GeneratorParams? entityToGenerate, IBvLogger? logger)
    {
        if (entityToGenerate != null)
        {
            return GenerateEntityClass(entityToGenerate, isNested: entityToGenerate?.ClassOnly ?? false);
        }

        return null;
    }

    public static string GenerateEntityClass(GeneratorParams entity, bool isNested = false)
    {
        isNested = entity.ClassOnly;
        var indentation = isNested ? "        " : "    ";
        var classIndentation = isNested ? "    " : "";

        IEnumerable<string> propertyStrings = entity.Properties.Select(p =>
        {
            var nullableSymbol = (p.IsNullable && !p.Type.EndsWith("?", StringComparison.OrdinalIgnoreCase)) ? "?" : string.Empty;
            var requiredAttribute = (p.IsRequired && string.IsNullOrEmpty(p.Expression)) ? $"\r\n{indentation}[Required]" : string.Empty;
            var validationAttributes = new List<string>();

#pragma warning disable MA0127 // Use String.Equals instead of is pattern
            if (string.Equals(p.Type, "string", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(p.Max))
                {
                    validationAttributes.Add($"[StringLength({p.Max})]");
                }

                if (!string.IsNullOrEmpty(p.Min))
                {
                    validationAttributes.Add($"[MinLength({p.Min})]");
                }

                if (!string.IsNullOrEmpty(p.Regex))
                {
                    validationAttributes.Add($"[RegularExpression(@\"{p.Regex}\")]");
                }
            }
            else if (p.Type is "int" or "long" or "decimal" or "float" or "double")
            {
                if (!string.IsNullOrEmpty(p.Max) || !string.IsNullOrEmpty(p.Min))
                {
                    validationAttributes.Add($"[Range({p.Min ?? "null"}, {p.Max ?? "null"})]");
                }
            }
            else if (string.Equals(p.Type, "DateTime", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(p.Min) || !string.IsNullOrEmpty(p.Max))
                {
                    var minDate = p.Min ?? "\"1900-01-01\"";
                    var maxDate = p.Max ?? "\"2100-12-31\"";
                    validationAttributes.Add($"[Range(typeof(DateTime), {minDate}, {maxDate})]");
                }
            }
#pragma warning restore MA0127 // Use String.Equals instead of is pattern

            var validationString = validationAttributes.Count > 0
                ? string.Join($"\r\n{indentation}", validationAttributes) + $"\r\n{indentation}"
                : string.Empty;

            var requiredString = (p.IsRequired && string.IsNullOrEmpty(p.Expression))
                ? "required "
                : string.Empty;

            var jsonPropertyAttributeString = !string.IsNullOrEmpty(p.JsonProperty)
                ? $"[JsonPropertyName(\"{p.JsonProperty}\")]\r\n{indentation}"
                : string.Empty;

            var jsonIgnoreAttribute = !string.IsNullOrEmpty(p.Expression) && string.IsNullOrEmpty(p.JsonProperty)
                ? $"[JsonIgnore]\r\n{indentation}"
                : string.Empty;

            var accessorType = p.IsSettable ? "set" : "init";

            var documentationString = !string.IsNullOrEmpty(p.Documentation)
                ? $"\r\n{indentation}/// <summary>\r\n{indentation}/// {(p.IsSettable ? "Gets or sets" : "Gets")} the {p.Name} property.\r\n{indentation}/// </summary>\r\n{indentation}/// <remarks>\r\n{indentation}/// {p.Documentation}\r\n{indentation}/// </remarks>"
                : $"\r\n{indentation}/// <summary>\r\n{indentation}/// {(p.IsSettable ? "Gets or sets" : "Gets")} the {p.Name} property.\r\n{indentation}/// </summary>";

            if (!string.IsNullOrEmpty(p.Expression))
            {
                return $@"{documentationString}{requiredAttribute}
{indentation}{jsonIgnoreAttribute}{validationString}public {requiredString}{p.Type}{nullableSymbol} {p.Name} => {p.Expression};";
            }

            return $@"{documentationString}{requiredAttribute}
{indentation}{jsonPropertyAttributeString}{jsonIgnoreAttribute}{validationString}public {requiredString}{p.Type}{nullableSymbol} {p.Name} {{ get; {accessorType}; }}";
        });

        var propertiesString = string.Join("\r\n", propertyStrings);
        var validatorClass = GenerateValidatorClass(entity, isNested);

        IEnumerable<string> nestedEntityClasses = entity.NestedEntities.Select(e => GenerateEntityClass(e, isNested: true));
        var nestedEntitiesString = string.Join("\r\n\r\n", nestedEntityClasses);

        var classDocumentation = !string.IsNullOrEmpty(entity.Documentation)
            ? $"\r\n{classIndentation}/// <summary>\r\n{classIndentation}/// {entity.Documentation}\r\n{classIndentation}/// </summary>\r\n"
            : string.Empty;

        var accessibility = string.IsNullOrEmpty(entity.Accessibility) ? "public" : entity.Accessibility;
        var abstractModifier = entity.Abstract ?? string.Empty;
        var inheritsClause = entity.Inherits ?? string.Empty;

        var classDefinition = $$"""
{{classIndentation}}{{classDocumentation}}{{accessibility}}{{abstractModifier}} partial record {{entity.Name}}{{inheritsClause}}
{{classIndentation}}{
{{propertiesString}}

{{classIndentation}}    // Static validator instance
{{classIndentation}}    public static readonly {{entity.Name}}Validator Validator = new {{entity.Name}}Validator();

{{classIndentation}}    // Instance method for validation
{{classIndentation}}    public void Validate()
{{classIndentation}}    {
{{classIndentation}}        var validationResult = Validator.Validate(this);
{{classIndentation}}        if (!validationResult.IsValid)
{{classIndentation}}        {
{{classIndentation}}            throw new FluentValidation.ValidationException(validationResult.ToString());
{{classIndentation}}        }
{{classIndentation}}    }

{{classIndentation}}    // Method to check validation without throwing exceptions
{{classIndentation}}    public FluentValidation.Results.ValidationResult GetValidationResult()
{{classIndentation}}    {
{{classIndentation}}        return Validator.Validate(this);
{{classIndentation}}    }

{{validatorClass}}

{{nestedEntitiesString}}
{{classIndentation}}}
""";

        if (!isNested)
        {
            return $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{entity.Namespace}};

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

{{classDefinition}}
""";
        }
        else
        {
            return classDefinition;
        }
    }

    private static string GenerateValidatorClass(GeneratorParams entity, bool isNested = false)
    {
        var indentation = isNested ? "            " : "        ";
        var classIndentation = isNested ? "        " : "    ";

        IEnumerable<string> validationRules = entity.Properties
            .Where(p => string.IsNullOrEmpty(p.Expression))
            .Select(p =>
            {
                var rules = new List<string>();
                var ruleBuilder = $"RuleFor(x => x.{p.Name})";

                if (p.IsRequired)
                {
                    rules.Add($"{ruleBuilder}.NotNull().WithMessage(\"'{p.Name}' is required.\");");
                }

#pragma warning disable MA0127 // Use String.Equals instead of is pattern
                if (string.Equals(p.Type, "string", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(p.Max)) rules.Add($"{ruleBuilder}.MaximumLength({p.Max}).WithMessage(\"'{p.Name}' must not exceed {p.Max} characters.\");");
                    if (!string.IsNullOrEmpty(p.Min)) rules.Add($"{ruleBuilder}.MinimumLength({p.Min}).WithMessage(\"'{p.Name}' must be at least {p.Min} characters.\");");
                    if (!string.IsNullOrEmpty(p.Regex)) rules.Add($"{ruleBuilder}.Matches(@\"{p.Regex}\").WithMessage(\"'{p.Name}' has an invalid format.\");");
                }
                else if (p.Type is "int" or "long" or "decimal" or "float" or "double")
                {
                    if (!string.IsNullOrEmpty(p.Min)) rules.Add($"{ruleBuilder}.GreaterThanOrEqualTo({p.Min}).WithMessage(\"'{p.Name}' must be at least {p.Min}.\");");
                    if (!string.IsNullOrEmpty(p.Max)) rules.Add($"{ruleBuilder}.LessThanOrEqualTo({p.Max}).WithMessage(\"'{p.Name}' must be no more than {p.Max}.\");");
                }
                else if (string.Equals(p.Type, "DateTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(p.Min)) rules.Add($"{ruleBuilder}.GreaterThanOrEqualTo(DateTime.Parse(\"{p.Min}\")).WithMessage(\"'{p.Name}' must be after {p.Min}.\");");
                    if (!string.IsNullOrEmpty(p.Max)) rules.Add($"{ruleBuilder}.LessThanOrEqualTo(DateTime.Parse(\"{p.Max}\")).WithMessage(\"'{p.Name}' must be before {p.Max}.\");");
                }
                else if (string.Equals(p.Type, "Guid", StringComparison.OrdinalIgnoreCase) && p.NoDefault)
                {
                    rules.Add($"{ruleBuilder}.NotEqual(Guid.Empty).WithMessage(\"'{p.Name}' must not be the default Guid.\");");
                }
#pragma warning restore MA0127 // Use String.Equals instead of is pattern

                return string.Join($"\r\n{indentation}", rules);
            })
            .Where(r => !string.IsNullOrEmpty(r));

        var rulesString = string.Join($"\r\n\r\n{indentation}", validationRules);

        return $$"""
{{classIndentation}}    // Internal FluentValidator class for validation rules
{{classIndentation}}    public partial class {{entity.Name}}Validator : AbstractValidator<{{entity.Name}}>
{{classIndentation}}    {
{{classIndentation}}        public {{entity.Name}}Validator()
{{classIndentation}}        {
{{indentation}}{{rulesString}}

{{indentation}}            // Hooks for additional validation
{{indentation}}            AddCustomValidation();
{{classIndentation}}        }

{{classIndentation}}        // Partial method for additional validation hooks
{{classIndentation}}        partial void AddCustomValidation();
{{classIndentation}}    }
""";
    }

    public record class GeneratorParams // Changed from readonly record struct to record class
    {
        public string Name { get; set; } = string.Empty; // Use init for immutability after creation
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string? ParentName { get; set; } // Added ParentName
        public string? Inherits { get; set; }
        public string? Abstract { get; set; }
        public string Accessibility { get; set; } = "public";
        public List<PropertyDescriptor> Properties { get; set; } = new();
        public List<GeneratorParams> NestedEntities { get; set; } = new(); // Use init
        public string? Documentation { get; set; }
        public bool ClassOnly { get; set; } // Indicates if it should only generate the class content

        /// <summary>
        /// Indicates if this DTO definition originated from a <ViewModelOwnedType>
        /// in the XML definition, signifying it should be treated as a nested type
        /// within its containing ViewModel for reflection/typeof purposes (Namespace.Outer+Inner).
        /// </summary>
        public bool IsNestedViewModelOwnedType { get; set; } = false; // Default to false

        public GeneratorParams(
            string name,
            string ns,
            string? parentName, // Added parentName parameter
            string? inherits,
            bool isAbstract,
            string? accessibility,
            List<PropertyDescriptor> properties,
            List<GeneratorParams>? nestedEntities = null,
            string? documentation = null,
            bool classOnly = false) // Added classOnly parameter
        {
            this.Name = name;
            this.Namespace = ns;
            this.ParentName = parentName; // Store parentName
            this.Inherits = inherits != null ? $" : {inherits}" : string.Empty;
            this.Abstract = isAbstract ? " abstract" : string.Empty;
            this.Accessibility = string.IsNullOrEmpty(accessibility) ? "public" : accessibility!;
            this.Properties = properties;
            this.NestedEntities = nestedEntities ?? []; // Initialize if null
            // Update FullyQualifiedName calculation
            this.FullyQualifiedName = string.IsNullOrEmpty(parentName)
                ? string.Join(".", ns, name)
                : string.Join(".", ns, parentName, name); // Include parent name
            this.Documentation = documentation;
            this.ClassOnly = classOnly; // Store classOnly

            if (!string.IsNullOrEmpty(parentName))
            {
                this.IsNestedViewModelOwnedType = true;
            }
        }
    }

    public readonly record struct PropertyDescriptor
    {
        public readonly string Name;
        public readonly string Type;
        public readonly bool IsRequired;
        public readonly bool IsNullable;
        public readonly string? Max;
        public readonly string? Min;
        public readonly string? Regex;
        public readonly string? JsonProperty;
        public readonly bool NoDefault;
        public readonly bool IsSettable;
        public readonly string? Expression;
        public readonly string? Documentation;

        public PropertyDescriptor(
            string name,
            string type,
            bool isRequired,
            bool isNullable,
            string? max,
            string? min,
            string? regex,
            string? jsonProperty,
            bool noDefault,
            bool isSettable,
            string? expression = null,
            string? documentation = null)
        {
            this.Name = name;
            this.Type = type;
            this.IsRequired = isRequired;
            this.IsNullable = isNullable;
            this.Max = max;
            this.Min = min;
            this.Regex = regex;
            this.JsonProperty = jsonProperty;
            this.NoDefault = noDefault;
            this.IsSettable = isSettable;
            this.Expression = expression;
            this.Documentation = documentation;
        }
    }
}
