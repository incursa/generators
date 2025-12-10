# DTO Generator Property Validation Rules

This document describes the validation rules implemented in the DTO generator for property configurations.

## Overview

The DTO generator validates property configurations based on the combination of `required`, `nullable`, `defaultValue`, and `settable` flags. These validations ensure that DTOs have clear and consistent semantics.

## Validation Rules

### For Normal (Non-Expression) Properties

#### Hard Invalid Configurations

These configurations will produce **ERROR** diagnostics:

1. **Case #1**: `required=false`, `nullable=false`, no `defaultValue`
   - **Issue**: Non-nullable type that's not required and has no default can stay null at runtime without validation catching it.
   - **Error Message**: "Invalid configuration for property '{name}': Non-nullable, non-required properties must have a default value. Either set required=true, nullable=true, or provide a defaultValue."
   - **Fix**: Add one of: `required: true`, `nullable: true`, or `defaultValue: "<value>"`

2. **Case #8**: `required=true`, `nullable=true`, `defaultValue` present
   - **Issue**: Combination has confusing semantics - "required + nullable + default"
   - **Error Message**: "Invalid configuration for property '{name}': Properties cannot be both required and nullable with a default value. This combination has unclear semantics. Remove one of: required, nullable, or defaultValue."
   - **Fix**: Remove one of the three conflicting flags

#### Discouraged Configurations

These configurations will produce **WARNING** diagnostics:

3. **Case #6**: `required=true`, `nullable=false`, `defaultValue` present
   - **Issue**: Default makes it effectively "non-nullable with default", but `required` suggests "must be provided by caller"
   - **Warning Message**: "Discouraged configuration for property '{name}': Required non-nullable properties should not have a default value. If the property is required, the caller should provide it. Either remove 'required' or remove 'defaultValue'."
   - **Fix**: Remove either `required` flag or `defaultValue`

4. **Case #7**: `required=true`, `nullable=true`, no `defaultValue`
   - **Issue**: Type says nullable but validation says non-nullable - logically inconsistent
   - **Warning Message**: "Inconsistent configuration for property '{name}': Required properties should not be nullable. The type allows null but validation requires non-null. Either remove 'required' or remove 'nullable'."
   - **Fix**: Remove either `required` or `nullable`

### For Strict DTOs

5. **Strict DTO with `settable=true`**
   - **Issue**: Strict DTOs should be immutable with `init`-only properties
   - **Error Message**: "Strict DTOs cannot have settable properties. Property '{name}' has settable=true in a strict DTO."
   - **Fix**: Remove `settable: true` or set `strict: false`

## Valid Configurations

The following property configurations are valid:

| Case | required | nullable | defaultValue | Description |
|------|----------|----------|--------------|-------------|
| #2   | false    | false    | present      | Optional non-nullable with default |
| #3   | false    | true     | absent       | Optional nullable |
| #4   | false    | true     | present      | Optional nullable with default |
| #5   | true     | false    | absent       | Required non-nullable (canonical) |

## Expression Properties

Properties with an `expression` value are skipped from validation as they are computed properties.

## Example Configurations

### Valid Examples

```json
{
  "properties": [
    {
      "name": "RequiredName",
      "type": "string",
      "required": true,
      "nullable": false,
      "documentation": "Case #5: Required non-nullable"
    },
    {
      "name": "OptionalWithDefault",
      "type": "string",
      "required": false,
      "nullable": false,
      "defaultValue": "\"default\"",
      "documentation": "Case #2: Optional non-nullable with default"
    },
    {
      "name": "OptionalNullable",
      "type": "string",
      "required": false,
      "nullable": true,
      "documentation": "Case #3: Optional nullable"
    }
  ]
}
```

### Invalid Examples

```json
{
  "properties": [
    {
      "name": "BadProperty1",
      "type": "string",
      "required": false,
      "nullable": false,
      "documentation": "ERROR: Case #1 - missing required defaultValue"
    },
    {
      "name": "BadProperty8",
      "type": "string",
      "required": true,
      "nullable": true,
      "defaultValue": "\"default\"",
      "documentation": "ERROR: Case #8 - conflicting flags"
    }
  ]
}
```

### Strict DTO Example

```json
{
  "name": "StrictDto",
  "namespace": "MyApp.Dtos",
  "strict": true,
  "properties": [
    {
      "name": "Id",
      "type": "Guid",
      "required": true,
      "settable": false,
      "documentation": "Valid: strict DTO with init-only property"
    }
  ]
}
```

## Implementation Details

- Validation is performed during JSON parsing in `DtoEntitySourceGenerator.ParseGeneratorParamsFromJson()`
- Errors are reported via `GeneratorDiagnostics.ReportValidationError()`
- The validation logic is in `DtoEntitySourceGenerator.ValidatePropertyConfiguration()`
- Validation is skipped when `productionContext` is null (CLI usage)
