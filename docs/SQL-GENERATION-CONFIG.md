# SQL Generation Configuration

The SQL Entity Generator now supports configuration files to customize the generation process, including setting the namespace for generated entities instead of using the hardcoded default.

## Configuration File Format

SQL Generation configuration files should follow the naming pattern `*SqlGeneration*.xml` and contain XML with the following structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<SqlGenerationConfiguration>
  <!-- The namespace to use for generated entities -->
  <Namespace>MyProject.Entities</Namespace>
  
  <!-- Whether to generate navigation properties between related entities -->
  <GenerateNavigationProperties>true</GenerateNavigationProperties>
  
  <!-- Whether to generate a DbContext class -->
  <GenerateDbContext>true</GenerateDbContext>
  
  <!-- The base class for the DbContext -->
  <DbContextBaseClass>IncursaDbContextBase</DbContextBaseClass>
</SqlGenerationConfiguration>
```

## Configuration Properties

### Namespace

- **Type**: `string`
- **Default**: `"Generated.Entities"`
- **Description**: Specifies the namespace to use for all generated entity classes.

### GenerateNavigationProperties

- **Type**: `boolean`
- **Default**: `true`
- **Description**: Controls whether navigation properties are generated between related entities.

### GenerateDbContext

- **Type**: `boolean`
- **Default**: `true`
- **Description**: Controls whether a DbContext class is generated for the entities.

### DbContextBaseClass

- **Type**: `string`
- **Default**: `"IncursaDbContextBase"`
- **Description**: Specifies the base class to use for the generated DbContext.
- **Default**: `true`
- **Description**: Controls whether navigation properties are generated between related entities.

## Usage

1. Create a configuration file (e.g., `SqlGenerationConfig.xml`) in your project directory
2. Set the desired namespace and other properties
3. Run the SQL Entity Generator - it will automatically detect and use the configuration file

## File Detection

The generator looks for files matching these patterns:

- `*.sql` - SQL schema files
- `*TypeMapping*.xml` - Type mapping configuration files  
- `*SqlGeneration*.xml` - SQL generation property files (new)

## Example

Given this configuration file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<SqlGenerationConfiguration>
  <Namespace>MyCompany.Data.Entities</Namespace>
  <GenerateNavigationProperties>true</GenerateNavigationProperties>
</SqlGenerationConfiguration>
```

All generated entity classes will be placed in the `MyCompany.Data.Entities` namespace instead of the default `Generated.Entities`.

## Priority

If multiple SQL generation configuration files are found, the first valid namespace configuration will be used. The generator will fall back to the default namespace (`Generated.Entities`) if no valid configuration is found.
