# Incursa.Generators Documentation

This repository contains a Roslyn source generator package (`src/Incursa.Generators`) that reads definition files from `AdditionalFiles` and emits C# `.g.cs` files at build time.

## Repository Scope

Implemented JSON generators in this repository:

| Generator | File suffix | Required JSON keys |
| --- | --- | --- |
| String-backed enum | `*.enum.json`, `*.string_enum.json` | `name`, `namespace`, `values` |
| Number-backed enum | `*.number_enum.json` | `name`, `namespace`, `values` |
| String-backed type | `*.string.json` | `name`, `namespace` |
| FastId-backed type | `*.fastid.json` | `name`, `namespace` |
| Guid-backed type | `*.guid.json` | `name`, `namespace` |
| Multi-value backed type | `*.multi.json` | `name`, `namespace` |
| DTO/entity | `*.dto.json`, `*.entity.json` | `name`, `namespace` |

Notes:

- This repo does not currently include a maintained CLI entrypoint. Use package/source-generator integration.
- Legacy ERP/SQL schema docs exist in `docs/schemas`, but they are not consumed by the generators in this codebase snapshot.

## How Input Files Are Discovered

`Incursa.Generators.props` auto-registers these files under `AdditionalFiles` for SDK-style projects:

- `**\*.enum.json`
- `**\*.string_enum.json`
- `**\*.number_enum.json`
- `**\*.guid.json`
- `**\*.string.json`
- `**\*.fastid.json`
- `**\*.multi.json`
- `**\*.dto.json`
- `**\*.entity.json`
- `**\_generate.xml`

It also includes `**\*.fast.json`, but there is no matching JSON source generator for that suffix in this repository.

## Quick Start (Package)

```xml
<ItemGroup>
  <PackageReference Include="Incursa.Generators" Version="x.x.x" PrivateAssets="all" />
</ItemGroup>
```

1. Add JSON definition files in your project using supported suffixes.
2. Build the project.
3. Consume generated types from emitted `.g.cs` files.

## JSON Authoring Rules

- Use exact property names from the contract below.
- Keep type names C#-valid (`PascalCase` identifiers are recommended).
- Keep namespace values valid C# namespaces.
- Avoid unknown keys even when parser ignores them.
- Prefer validating with schemas in `docs/schemas` before build.

## DTO/Entity JSON (Detailed)

Source of truth: `src/Incursa.Generators/DtoEntitySourceGenerator.cs` and `src/Incursa.Generators/CoreGenerators/DtoEntityGenerator.cs`.

### Top-Level Fields

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `name` | string | required | DTO/entity type name |
| `namespace` | string | required for root | Optional for nested entities; inherited from parent |
| `documentation` | string | null | XML summary text |
| `inherits` | string | null | Base type / interfaces string used as-is |
| `accessibility` | string | `"public"` | Recommended: `public`, `internal`, `protected`, `private` |
| `abstract` | bool | `false` | Invalid with `strict: true` |
| `classOnly` | bool | `false` | Generates class content form used for nesting scenarios |
| `strict` | bool | `false` | Enforces strict DTO constraints |
| `useParentValidator` | bool | `true` | Validator inheritance behavior |
| `noCreateMethod` | bool | `false` | Suppresses `Create(...)` factory generation |
| `isRecordStruct` | bool | `false` | Invalid with `abstract: true` or non-empty `inherits` |
| `properties` | array | empty | Property definitions |
| `nestedEntities` | array | empty | Recursive nested DTO/entity definitions |

### Property Fields

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `name` | string | required | Property name |
| `type` | string | required | C# type string |
| `required` | bool | `true` | If omitted and `defaultValue` exists, effective required becomes `false` |
| `nullable` | bool | `false` | Adds nullable marker when needed |
| `settable` | bool | `false` | `false` => `init`; `true` => `set` |
| `noDefault` | bool | `false` | Prevents default initializer usage |
| `max` | string | null | Validation attribute input |
| `min` | string | null | Validation attribute input |
| `regex` | string | null | Validation attribute input |
| `jsonProperty` | string | null | Emits `[JsonPropertyName]` |
| `defaultValue` | string | null | C# initializer expression |
| `expression` | string | null | Computed property expression |
| `documentation` | string | null | Property XML docs |

### DTO Validation Semantics

Validation logic runs during source generation diagnostics (`BG004`, `BG006`) and is skipped for expression properties.

Hard errors (`BG004`):

- `strict: true` with any property using `settable: true`
- `required: false`, `nullable: false`, no effective default, for reference types
- `required: true`, `nullable: true`, with effective default

Warnings (`BG006`):

- `required: true`, `nullable: false`, with effective default
- `required: true`, `nullable: true`, without effective default

Effective default means `defaultValue` exists and `noDefault` is not true.

### DTO Example (Valid)

```json
{
  "name": "CreateUserDto",
  "namespace": "MyApp.Contracts.Users",
  "strict": true,
  "properties": [
    {
      "name": "Id",
      "type": "Guid",
      "required": true,
      "settable": false,
      "documentation": "User identifier"
    },
    {
      "name": "Email",
      "type": "string",
      "required": true,
      "regex": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
      "jsonProperty": "email"
    },
    {
      "name": "DisplayName",
      "type": "string",
      "defaultValue": "\"Unknown\""
    },
    {
      "name": "NormalizedEmail",
      "type": "string",
      "expression": "Email.ToUpperInvariant()",
      "jsonProperty": "normalized_email"
    }
  ],
  "nestedEntities": [
    {
      "name": "Metadata",
      "properties": [
        {
          "name": "CreatedBy",
          "type": "string",
          "required": true
        }
      ]
    }
  ]
}
```

### DTO Invalid Examples

`strict` + mutable property:

```json
{
  "name": "BadStrict",
  "namespace": "MyApp.Contracts",
  "strict": true,
  "properties": [
    {
      "name": "Name",
      "type": "string",
      "settable": true
    }
  ]
}
```

Optional non-nullable reference without default:

```json
{
  "name": "BadOptional",
  "namespace": "MyApp.Contracts",
  "properties": [
    {
      "name": "Name",
      "type": "string",
      "required": false,
      "nullable": false
    }
  ]
}
```

## String-Backed Type JSON (Detailed)

Source of truth: `src/Incursa.Generators/StringBackedTypeSourceGenerator.cs` and `src/Incursa.Generators/CoreGenerators/StringBackedTypeGenerator.cs`.

### Fields

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `name` | string | required | Type name |
| `namespace` | string | required | Namespace |
| `regex` | string | null | Inline regex pattern |
| `regexConst` | string | null | Name/expression of regex constant in generated code |
| `properties` | array | empty | Additional parsed properties; each requires both `name` and `type` |

Branch behavior:

- If `regexConst` is present, generator uses regex-constant branch.
- Else if `regex` is present, generator uses inline regex branch.
- Else generator uses non-regex branch.

### String-Backed Example (Regex)

```json
{
  "name": "EmailAddress",
  "namespace": "MyApp.Domain.Types",
  "regex": "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}"
}
```

### String-Backed Example (Regex Constant)

```json
{
  "name": "PhoneNumber",
  "namespace": "MyApp.Domain.Types",
  "regexConst": "PhoneValidationPatterns.E164",
  "properties": [
    {
      "name": "CountryCode",
      "type": "string"
    }
  ]
}
```

### String-Backed Invalid Example

Incomplete additional property (fails parsing):

```json
{
  "name": "BadType",
  "namespace": "MyApp.Domain.Types",
  "properties": [
    {
      "name": "OnlyName"
    }
  ]
}
```

## Other JSON Types (Concise Templates)

### String-Backed Enum (`*.enum.json`, `*.string_enum.json`)

```json
{
  "name": "Color",
  "namespace": "MyApp.Domain.Enums",
  "values": {
    "Red": { "value": "red", "display": "Red" },
    "Blue": { "value": "blue" }
  },
  "properties": {
    "Hex": { "type": "string" }
  }
}
```

### Number-Backed Enum (`*.number_enum.json`)

```json
{
  "name": "Priority",
  "namespace": "MyApp.Domain.Enums",
  "type": "int",
  "values": {
    "Low": { "value": 1 },
    "High": { "value": 2, "display": "High" }
  }
}
```

### FastId-Backed Type (`*.fastid.json`)

```json
{
  "name": "OrderId",
  "namespace": "MyApp.Domain.Ids"
}
```

### Guid-Backed Type (`*.guid.json`)

```json
{
  "name": "CustomerId",
  "namespace": "MyApp.Domain.Ids",
  "defaultFormat": true
}
```

### Multi-Value Backed Type (`*.multi.json`)

```json
{
  "name": "CompositeCode",
  "namespace": "MyApp.Domain.Types",
  "separator": "|",
  "parts": [
    { "name": "Region", "type": "string" },
    { "name": "Sequence", "type": "int" }
  ]
}
```

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `BG001` | Error | Generator error/exception |
| `BG002` | Warning | Generation skipped for a file |
| `BG003` | Warning | Duplicate hint name detected |
| `BG004` | Error | Validation error |
| `BG005` | Error | Missing required property |
| `BG006` | Warning | Validation warning |

## Schema Validation

Use the schemas in `docs/schemas` for authoring and pre-build validation.

- Schema index: `docs/schemas/README.md`
- LLM JSON contract: `docs/LLM_Natural_Natural_Writing_Guide.md`

