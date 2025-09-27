# Example Usage of SQL Entity Source Generator

This directory contains example files showing how to use the SQL Entity Source Generator.

## Sample Project Structure

```
MyProject/
├── Schema/
│   ├── Tables/
│   │   ├── Users.sql
│   │   └── Orders.sql
│   └── Views/
│       └── UserOrders.sql
├── MyProject.csproj
└── Program.cs
```

## Sample .csproj File

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Bravellian.Generators\Bravellian.Generators.csproj" 
                      OutputItemType="Analyzer" 
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="Schema/**/*.sql" SourceItemType="SqlSchemaFile" />
  </ItemGroup>

</Project>
```

## Sample SQL Files

### Schema/Tables/Users.sql
```sql
CREATE TABLE [dbo].[Users] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(255) NOT NULL UNIQUE,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NULL,
    [IsActive] BIT NOT NULL DEFAULT 1
);
```

### Schema/Tables/Orders.sql
```sql
CREATE TABLE [dbo].[Orders] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [OrderNumber] NVARCHAR(50) NOT NULL UNIQUE,
    [OrderDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [TotalAmount] DECIMAL(18,2) NOT NULL,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending'
);
```

### Schema/Views/UserOrders.sql
```sql
CREATE VIEW [dbo].[UserOrders] AS
SELECT 
    u.Id as UserId,
    u.FirstName,
    u.LastName,
    u.Email,
    o.Id AS OrderId,
    o.OrderNumber,
    o.OrderDate,
    o.TotalAmount,
    o.Status
FROM [dbo].[Users] u
INNER JOIN [dbo].[Orders] o ON u.Id = o.UserId
WHERE u.IsActive = 1;
```

## Sample Usage in Code

```csharp
// Program.cs
using Generated.Entities;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer("Server=.;Database=MyApp;Trusted_Connection=true;"));

var app = builder.Build();

// Use the generated entities
using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();

// Query users
var users = await context.Users.Where(u => u.IsActive).ToListAsync();

// Query orders with users
var userOrders = await context.UserOrders.ToListAsync();

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }
    
    // Generated entities
    public DbSet<UsersDbEntity> Users => Set<UsersDbEntity>();
    public DbSet<OrdersDbEntity> Orders => Set<OrdersDbEntity>();
    public DbSet<UserOrdersDbEntity> UserOrders => Set<UserOrdersDbEntity>();
}
```

## Build and Run

1. Create the project structure above
2. Run `dotnet build` to trigger the source generator
3. Check the generated files in your IDE under Dependencies > Analyzers
4. Use the generated entities in your DbContext and queries

The source generator will automatically create:
- `dbo_Users.g.cs` with `UsersDbEntity` class
- `dbo_Orders.g.cs` with `OrdersDbEntity` class  
- `dbo_UserOrders.g.cs` with `UserOrdersDbEntity` class

Each generated class will include proper Entity Framework attributes, type mappings, and nullable configurations based on your SQL schema.
