# Incursa.Generators

`Incursa.Generators` is a Roslyn source generator package for creating strongly typed domain models from JSON and XML definition files.

It is designed for teams that want consistency, type safety, and low-boilerplate patterns for identifiers, enums, and DTO-style models.

## What It Generates

- String-backed value types
- GUID-backed and FastId-backed identifiers
- Number-backed and string-backed enums
- Multi-value and generic-backed types
- DTO and entity models

## Installation

```xml
<ItemGroup>
  <PackageReference Include="Incursa.Generators" Version="x.x.x" PrivateAssets="all" />
</ItemGroup>
```

## Quick Start

1. Add definition files (for example `*.string.json`, `*.enum.json`, `*.dto.json`, `*.dto.xml`) to your project.
2. Build your project.
3. Use the generated `.g.cs` files as normal domain types.

Example string-backed type definition:

```json
{
  "name": "EmailAddress",
  "namespace": "MyApp.Domain",
  "regex": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
}
```

## Configuration

Optional custom header for generated code:

```xml
<PropertyGroup>
  <GeneratedCodeLicenseHeader>// Copyright (c) Your Company</GeneratedCodeLicenseHeader>
</PropertyGroup>
```

## Generated Type Characteristics

- Strongly typed and partial
- Built-in conversion and parsing patterns (where applicable)
- Validation-aware for supported definitions
- Clear diagnostics for invalid inputs

## Diagnostics

The package emits diagnostics (`BG00x`) for malformed definitions, duplicate outputs, and generation errors to help identify issues early during build.

## Compatibility

- Generator package target: `netstandard2.0`
- Consumer projects: modern SDK-style C# projects (including .NET 8+ and .NET 10)

## Documentation and Samples

- Repository docs and schema references: <https://github.com/incursa/generators/tree/main/docs>
- Sample inputs: <https://github.com/incursa/generators/tree/main/examples>

## Support

- Open an issue: <https://github.com/incursa/generators/issues>
