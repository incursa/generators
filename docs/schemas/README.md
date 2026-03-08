# Incursa Generator JSON Schemas

This folder contains JSON Schema files for definition files consumed by generators implemented in this repository.

## Active Schemas (Implemented Generators)

| Schema file | Input suffixes | Notes |
| --- | --- | --- |
| `string-backed-enum.schema.json` | `*.enum.json`, `*.string_enum.json` | `properties` is an object map keyed by property name |
| `number-backed-enum.schema.json` | `*.number_enum.json` | Numeric enum values |
| `string-backed-type.schema.json` | `*.string.json` | Uses `regex` and `regexConst` |
| `fastid-backed-type.schema.json` | `*.fastid.json` | Minimal contract: `name`, `namespace` |
| `guid-backed-type.schema.json` | `*.guid.json` | Supports optional `defaultFormat` |
| `multi-value-backed-type.schema.json` | `*.multi.json` | Uses `parts`, not `values` |
| `dto-entity.schema.json` | `*.dto.json`, `*.entity.json` | Includes strict DTO flags and nested entities |

## Legacy / External Schemas

The following schema files are present for historical or external integration references and are not consumed by the JSON source generators in this repository snapshot:

- `erp-capability-definitions.schema.json`
- `erp-capabilities-only.schema.json`
- `erp-adapter-profile.schema.json`
- `sql-config.schema.json`

## VS Code Setup

Add schema mappings in `settings.json`:

```json
{
  "json.schemas": [
    {
      "fileMatch": ["*.enum.json", "*.string_enum.json"],
      "url": "./docs/schemas/string-backed-enum.schema.json"
    },
    {
      "fileMatch": ["*.number_enum.json"],
      "url": "./docs/schemas/number-backed-enum.schema.json"
    },
    {
      "fileMatch": ["*.string.json"],
      "url": "./docs/schemas/string-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.fastid.json"],
      "url": "./docs/schemas/fastid-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.guid.json"],
      "url": "./docs/schemas/guid-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.multi.json"],
      "url": "./docs/schemas/multi-value-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.dto.json", "*.entity.json"],
      "url": "./docs/schemas/dto-entity.schema.json"
    }
  ]
}
```

## Authoring Guidance

- Prefer schema validation before build.
- Keep property names exact and case-sensitive.
- Use `docs/README.md` and `docs/LLM_Natural_Natural_Writing_Guide.md` for behavior details and examples.
