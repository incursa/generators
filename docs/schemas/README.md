# Bravellian Code Generator JSON Schemas

This directory contains JSON Schema files for validating definition files used with the Bravellian Code Generator CLI tool.

## Available Schemas

### String Backed Enums
- **File**: `string-backed-enum.schema.json`
- **Usage**: For `.enum.json` and `.string_enum.json` files
- **Description**: Defines string-backed enum types with optional additional properties

### Number Backed Enums
- **File**: `number-backed-enum.schema.json`
- **Usage**: For `.number_enum.json` files
- **Description**: Defines number-backed enum types with configurable underlying numeric types

### String Backed Types
- **File**: `string-backed-type.schema.json`
- **Usage**: For `.string.json` files
- **Description**: Defines validated string value types with optional regex patterns

### FastId Backed Types
- **File**: `fastid-backed-type.schema.json`
- **Usage**: For `.fastid.json` files
- **Description**: Defines FastId-backed identifier types

### GUID Backed Types
- **File**: `guid-backed-type.schema.json`
- **Usage**: For `.guid.json` files
- **Description**: Defines GUID-backed identifier types

### DTO/Entity Types
- **File**: `dto-entity.schema.json`
- **Usage**: For `.dto.json` and `.entity.json` files
- **Description**: Defines data transfer objects and entity classes with validation attributes

### ERP Capability Definitions

- **File**: `erp-capability-definitions.schema.json`
- **Usage**: For `.erp-capabilities.json` files
- **Description**: Defines ERP capability definitions and adapter profiles

### Multi-Value Types
- **File**: `multi-value-backed-type.schema.json`
- **Usage**: For `.multi.json` files
- **Description**: Defines types that can have multiple predefined values

## Using the Schemas

### In VS Code
1. Install the "JSON Schema Support" extension
2. Configure your `settings.json` to associate the schemas with file patterns:

```json
{
  "json.schemas": [
    {
      "fileMatch": ["*.enum.json", "*.string_enum.json"],
      "url": "./schemas/string-backed-enum.schema.json"
    },
    {
      "fileMatch": ["*.number_enum.json"],
      "url": "./schemas/number-backed-enum.schema.json"
    },
    {
      "fileMatch": ["*.string.json"],
      "url": "./schemas/string-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.fastid.json"],
      "url": "./schemas/fastid-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.guid.json"],
      "url": "./schemas/guid-backed-type.schema.json"
    },
    {
      "fileMatch": ["*.dto.json", "*.entity.json"],
      "url": "./schemas/dto-entity.schema.json"
    },
    {
      "fileMatch": ["*.erp-capabilities.json"],
      "url": "./schemas/erp-capability-definitions.schema.json"
    },
    {
      "fileMatch": ["*.multi.json"],
      "url": "./schemas/multi-value-backed-type.schema.json"
    }
  ]
}
```

### In Other Editors
Most modern editors with JSON support can use these schemas for validation and IntelliSense by referencing the schema URL in your JSON files:

```json
{
  "$schema": "./schemas/string-backed-enum.schema.json",
  "name": "YourEnum",
  "namespace": "Your.Namespace",
  "values": {
    // Your enum values here
  }
}
```

## Schema Features

All schemas provide:
- **Validation**: Ensures your definition files are correctly structured
- **IntelliSense**: Auto-completion for properties and values
- **Documentation**: Hover help for all properties
- **Examples**: Complete working examples for each type

## Contributing

When adding new generator types or modifying existing ones, please update the corresponding schema files to maintain validation accuracy.
