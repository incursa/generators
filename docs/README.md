# Bravellian Code Generators

This project contains source generators and CLI tools for generating C# code from various definition files.

## Supported Generators

### String Backed Enums
- **Files**: `*.enum.json`, `*.string_enum.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates string-backed enum types with optional additional properties

### Number Backed Enums  
- **Files**: `*.number_enum.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates number-backed enum types with configurable underlying numeric types

### String Backed Types
- **Files**: `*.string.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates validated string value types with optional regex patterns

### FastId Backed Types
- **Files**: `*.fastid.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates FastId-backed identifier types

### GUID Backed Types
- **Files**: `*.guid.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates GUID-backed identifier types

### DTO/Entity Types
- **Files**: `*.dto.json`, `*.entity.json`, `*.dto.xml`, `*.entities.xml`, `*.viewmodels.xml`
- **Description**: Generates data transfer objects and entity classes with validation attributes

### Multi-Value Types
- **Files**: `*.multi.json`, `*.types.xml`, `*.sbt.xml`
- **Description**: Generates types that can have multiple predefined values

### ERP Capability Definitions
- **Files**: `*.erp-capabilities.json`, `*.erp-capabilities.xml`, `_generate.xml`
- **Description**: Generates ERP capability definitions, interface definitions, and adapter profiles for ERP integration systems

### SQL Entity Generation
- **Files**: `*.sql`, `*TypeMapping*.xml`, `*SqlGeneration*.xml`
- **Description**: Generates entity classes from SQL schema definitions

## CLI Usage

```bash
# Generate from a single file
dotnet run --project Bravellian.Generators -- --input "definitions.json" --output "Generated"

# Generate from multiple files
dotnet run --project Bravellian.Generators -- --input "file1.json;file2.xml" --output "Generated"

# Generate from a directory
dotnet run --project Bravellian.Generators -- --input "DefinitionsFolder" --output "Generated"

# Dry run to see what would be generated
dotnet run --project Bravellian.Generators -- --input "definitions.json" --output "Generated" --dry-run

# Verbose output
dotnet run --project Bravellian.Generators -- --input "definitions.json" --output "Generated" --verbose
```

## JSON Schema Support

JSON schema files are available in the `schemas/` directory for validation and IntelliSense support in editors. See [schemas/README.md](schemas/README.md) for setup instructions.

## Examples

Example definition files are available for each generator type:
- String enums: `TestEnum.enum.json`
- DTO entities: `TestDtoExample.dto.xml`
- ERP capabilities: See attachments for XML examples

## Building

```bash
# Build the entire solution
dotnet build

# Build just the CLI tool
dotnet build Bravellian.Generators

# Run tests
dotnet test
```