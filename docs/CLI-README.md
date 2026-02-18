# Incursa Code Generator CLI Tool

This is a .NET command-line tool that replaces the Roslyn source generator functionality to generate C# code from definition files.

## Installation

`Bravellian.Generators` remains the published NuGet package ID for compatibility.

To install the tool globally:

```bash
dotnet tool install --global Bravellian.Generators
```

Or to install it from a local build:

```bash
cd Incursa.Generators
dotnet pack
dotnet tool install --global --add-source ./bin/Debug Bravellian.Generators
```

## Usage

```bash
# Single directory input
dotnet tool run incursa-gen --input ./types --output ./Generated

# Multiple file inputs
dotnet tool run incursa-gen --input ./types/Color.enum.json ./types/User.dto.xml --output ./Generated

# Mixed file and directory inputs
dotnet tool run incursa-gen --input ./types/specific-file.json ./other-types --output ./Generated
```

### Command Line Arguments

- `--input, -i` (Required): One or more input paths (files or directories) containing definition files. Supports multiple values.
- `--output, -o` (Required): Output directory for generated files  
- `--dry-run, -d`: Show what files would be generated without writing them
- `--verbose, -v`: Enable verbose logging

### Example

```bash
# Generate code from types in ./definitions to ./Generated folder
incursa-gen --input ./definitions --output ./Generated

# Dry run to see what would be generated
incursa-gen --input ./definitions --output ./Generated --dry-run --verbose
```

## Supported File Types

The tool supports the following file types for code generation. JSON Schema files are available in the `schemas/` directory for validation and IntelliSense support.

### String Backed Enums

- `.enum.json` - JSON format for string enums
- `.string_enum.json` - Alternative JSON format 
- `.types.xml` - XML format with StringEnum elements

**Schema**: `schemas/string-backed-enum.schema.json`

### Number Backed Enums  

- `.number_enum.json` - JSON format for number enums

**Schema**: `schemas/number-backed-enum.schema.json`

### String Backed Types

- `.string.json` - JSON format for string types

**Schema**: `schemas/string-backed-type.schema.json`

### ID Types

- `.fastid.json` - JSON format for FastId types
- `.guid.json` - JSON format for GUID types

**Schemas**: `schemas/fastid-backed-type.schema.json`, `schemas/guid-backed-type.schema.json`

### DTO/Entity Types

- `.dto.xml` - XML format for DTOs
- `.dto.json` - JSON format for DTOs
- `.entity.json` - JSON format for entities

**Schema**: `schemas/dto-entity.schema.json`

### Multi-Value Types

- `.multi.json` - JSON format for multi-value types

**Schema**: `schemas/multi-value-backed-type.schema.json`

### Generic XML Files

- `.types.xml` - Generic XML files that can contain various type definitions
- `_generate.xml` - Special XML files for generation

## Generators Included

The tool includes the following generators:

1. **StringBackedEnumTypeGenerator** - Generates string-backed enums
2. **StringBackedTypeGenerator** - Generates string-backed types
3. **DtoEntityGenerator** - Generates DTO and entity classes
4. **FastIdBackedTypeGenerator** - Generates FastId-backed types
5. **GuidBackedTypeGenerator** - Generates GUID-backed types  
6. **GenericBackedTypeGenerator** - Generates generic-backed types
7. **MultiValueBackedTypeGenerator** - Generates multi-value types
8. **NumberBackedEnumTypeGenerator** - Generates number-backed enums
9. **SqlEntityGenerator** - Generates SQL entities from SQL schema files

### SQL Entity Generation

The SQL Entity Generator supports configuration files for customizing the generation process:

- **SQL Files**: `*.sql` - Database schema definitions
- **Type Mapping**: `*TypeMapping*.xml` - Custom type mappings for columns
- **Generation Config**: `*SqlGeneration*.xml` - **NEW** Generation properties like namespace

Example SQL generation configuration (`SqlGenerationConfig.xml`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<SqlGenerationConfiguration>
  <Namespace>MyProject.Data.Entities</Namespace>
  <GenerateNavigationProperties>true</GenerateNavigationProperties>
</SqlGenerationConfiguration>
```

This allows you to specify the namespace for generated entities instead of using the default `Generated.Entities`.

## Output

The tool:

- Creates or clears the output directory
- Generates `.g.cs` files for each type
- Provides progress logging and error reporting
- Supports dry-run mode to preview changes
- Returns exit code 1 if any errors occur

## Examples

### String Enum JSON Format

```json
{
  "name": "Color",
  "namespace": "MyApp.Enums",
  "values": {
    "Red": {
      "value": "red",
      "display": "Red Color",
      "documentation": "The color red"
    },
    "Blue": {
      "value": "blue", 
      "display": "Blue Color"
    }
  },
  "properties": {
    "HexCode": {
      "type": "string"
    }
  }
}
```

This will generate a `MyApp.Enums.Color.g.cs` file with a string-backed enum.

## Development

To build and test locally:

```bash
# Build the solution
dotnet build

# Run tests  
dotnet test

# Package the CLI tool
cd Incursa.Generators
dotnet pack

# Install locally for testing
dotnet tool uninstall --global Bravellian.Generators
dotnet tool install --global --add-source ./bin/Debug Bravellian.Generators
```
