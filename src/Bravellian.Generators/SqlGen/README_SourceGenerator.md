# SQL Entity Source Generator

This source generator automatically creates C# entity classes from SQL schema files during compilation.

## Setup

### 1. Add SQL Files to Your Project

In your `.csproj` file, add your SQL schema files as `AdditionalFiles` with the `SqlSchemaFile` source item type:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schema/**/*.sql" SourceItemType="SqlSchemaFile" />
</ItemGroup>
```

### 2. Add Type Mapping Configuration (Optional)

You can optionally include XML files to configure custom type mappings:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schema/**/*.sql" SourceItemType="SqlSchemaFile" />
  <AdditionalFiles Include="TypeMappings.xml" SourceItemType="SqlTypeMappingConfig" />
</ItemGroup>
```

### 3. Project Structure

Organize your SQL files in a directory structure like this:

```
YourProject/
├── Schema/
│   ├── Tables/
│   │   ├── Users.sql
│   │   ├── Orders.sql
│   │   └── Products.sql
│   └── Views/
│       ├── UserOrders.sql
│       └── ProductSummary.sql
└── YourProject.csproj
```

### 3. SQL File Format

The generator supports standard SQL DDL statements for tables and views:

```sql
-- Users.sql
CREATE TABLE [dbo].[Users] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(255) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NULL
);
```

```sql
-- UserOrders.sql
CREATE VIEW [dbo].[UserOrders] AS
SELECT 
    u.Id as UserId,
    u.FirstName + ' ' + u.LastName AS FullName,
    o.Id AS OrderId,
    o.OrderDate,
    o.TotalAmount
FROM [dbo].[Users] u
INNER JOIN [dbo].[Orders] o ON u.Id = o.UserId;
```

## Generated Output

The source generator will create entity classes in the `Generated.Entities` namespace:

```csharp
// Generated/dbo_Users.g.cs
namespace Generated.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("Users", Schema = "dbo")]
[PrimaryKey(nameof(Id))]
internal partial record UsersDbEntity
{
    [Column("Id", TypeName = "UNIQUEIDENTIFIER")]
    [Required]
    public required Guid Id { get; set; }

    [Column("FirstName", TypeName = "NVARCHAR")]
    [Required]
    [StringLength(100)]
    public required string FirstName { get; set; }

    [Column("LastName", TypeName = "NVARCHAR")]
    [Required]
    [StringLength(100)]
    public required string LastName { get; set; }

    [Column("Email", TypeName = "NVARCHAR")]
    [Required]
    [StringLength(255)]
    public required string Email { get; set; }

    [Column("CreatedAt", TypeName = "DATETIME2")]
    [Required]
    public required DateTime CreatedAt { get; set; }

    [Column("UpdatedAt", TypeName = "DATETIME2")]
    public DateTime? UpdatedAt { get; set; }
}
```

## Features

- **Automatic Type Mapping**: SQL types are automatically mapped to appropriate C# types
- **Entity Framework Attributes**: Generated classes include proper EF Core attributes
- **Primary Key Detection**: Automatically detects and configures primary keys
- **Nullable Types**: Properly handles nullable columns
- **Schema Support**: Supports multiple database schemas
- **Views Support**: Generates entities for database views
- **Incremental Generation**: Only regenerates when SQL files change

## Supported SQL Features

- Tables with various column types
- Views with joins and expressions
- Primary key constraints (single and composite)
- NOT NULL constraints
- DEFAULT values
- Multiple schemas (dbo, custom schemas)

## Limitations

- No foreign key relationships (yet)
- No stored procedures
- SQL Server syntax only
- No custom type mappings (uses defaults)

## Troubleshooting

### Generator Not Running

If the generator isn't creating files:

1. Ensure your SQL files are marked as `AdditionalFiles` with `SourceItemType="SqlSchemaFile"`
2. Check the build output for any generator errors
3. Verify your SQL syntax is valid

### Build Errors

If you get compilation errors:

1. Check that generated entity names don't conflict with existing classes
2. Ensure required Entity Framework packages are referenced
3. Review SQL syntax for any unsupported features

### Finding Generated Files

Generated files are created in the compilation output and can be viewed:

1. In Visual Studio: Go to Dependencies > Analyzers > Bravellian.Generators > Bravellian.Generators.SqlGen.SqlEntitySourceGenerator
2. Files are named using the pattern: `{Schema}_{TableName}.g.cs`

## Example Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Bravellian.Generators" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="Schema/**/*.sql" SourceItemType="SqlSchemaFile" />
  </ItemGroup>

</Project>
```
