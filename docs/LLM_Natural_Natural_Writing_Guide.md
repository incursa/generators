# LLM JSON Authoring Contract for Incursa.Generators

Use this guide when you need an LLM to produce generator definition JSON that compiles and generates code correctly.

## Output Requirements

- Return only valid JSON.
- Return one root JSON object per file.
- Do not include comments, markdown fences, or explanatory text.
- Do not invent unsupported fields.
- Use the exact property names and casing shown here.

## Global Rules

- `name` must be a valid C# type identifier (`PascalCase` recommended).
- `namespace` must be a valid C# namespace.
- Keep values schema-valid and parser-valid.
- Prefer the strict schemas in `docs/schemas`.
- Avoid extra keys even if parser might ignore them.

## Generator Selection

Choose the output file suffix and object shape together:

| Goal | Suffix | Root shape |
| --- | --- | --- |
| String enum | `*.enum.json` or `*.string_enum.json` | `name`, `namespace`, `values`, optional `properties` object |
| Number enum | `*.number_enum.json` | `name`, `namespace`, `values`, optional `type` |
| String-backed value object | `*.string.json` | `name`, `namespace`, optional `regex`/`regexConst` |
| FastId-backed identifier | `*.fastid.json` | `name`, `namespace` |
| Guid-backed identifier | `*.guid.json` | `name`, `namespace`, optional `defaultFormat` |
| Multi-value type | `*.multi.json` | `name`, `namespace`, optional `parts` and formatting fields |
| DTO/entity | `*.dto.json` or `*.entity.json` | `name`, `namespace`, optional `properties`, nested config |

## Canonical Templates

### DTO / Entity

```json
{
  "name": "SampleDto",
  "namespace": "MyApp.Contracts",
  "strict": false,
  "useParentValidator": true,
  "noCreateMethod": false,
  "isRecordStruct": false,
  "properties": [
    {
      "name": "Id",
      "type": "Guid",
      "required": true,
      "nullable": false,
      "settable": false
    }
  ],
  "nestedEntities": []
}
```

### String-Backed Type

```json
{
  "name": "EmailAddress",
  "namespace": "MyApp.Domain.Types",
  "regex": "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}",
  "properties": [
    {
      "name": "Domain",
      "type": "string"
    }
  ]
}
```

### String-Backed Enum

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

### Number-Backed Enum

```json
{
  "name": "Priority",
  "namespace": "MyApp.Domain.Enums",
  "type": "int",
  "values": {
    "Low": { "value": 1 },
    "High": { "value": 2 }
  }
}
```

### Guid-Backed Type

```json
{
  "name": "CustomerId",
  "namespace": "MyApp.Domain.Ids",
  "defaultFormat": true
}
```

### FastId-Backed Type

```json
{
  "name": "OrderId",
  "namespace": "MyApp.Domain.Ids"
}
```

### Multi-Value Backed Type

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

## DTO Rules (Critical)

- Property `required` defaults to `true` if omitted.
- Property `settable` defaults to `false` if omitted.
- If `required` is omitted and `defaultValue` exists, effective `required` becomes `false`.
- `strict: true` cannot be combined with any property using `settable: true`.
- Avoid `required: false` + `nullable: false` for reference types unless `defaultValue` is present.

## String-Backed Rules (Critical)

- Use `regex` for inline pattern text.
- Use `regexConst` for constant/expression reference.
- If both are present, generator behavior follows `regexConst` branch.
- In `properties`, each item must include both `name` and `type`.

## Invalid Examples

Invalid DTO (`strict` with mutable property):

```json
{
  "name": "BadDto",
  "namespace": "MyApp.Contracts",
  "strict": true,
  "properties": [
    { "name": "Name", "type": "string", "settable": true }
  ]
}
```

Invalid string-backed type (incomplete property):

```json
{
  "name": "BadType",
  "namespace": "MyApp.Domain.Types",
  "properties": [
    { "name": "OnlyName" }
  ]
}
```

Invalid string-backed enum (`properties` wrong shape):

```json
{
  "name": "BadEnum",
  "namespace": "MyApp.Enums",
  "values": { "One": { "value": "one" } },
  "properties": [
    { "name": "Code", "type": "string" }
  ]
}
```

## Pre-Return Checklist

- File suffix matches object shape.
- All required keys are present.
- No unsupported keys are present.
- DTO defaults/constraints are respected.
- String-backed `regex` vs `regexConst` is intentional.
- JSON validates against the matching file in `docs/schemas`.
