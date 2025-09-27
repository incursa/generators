# SQL to Entity Generator Refactoring

This document outlines the refactored architecture for the SQL to Entity Generator.

## Architecture

The generator is split into four distinct phases:

1. **SQL Ingestion** - Parse SQL files and create raw SQL model
2. **Type Resolution** - Resolve column types and nullability
3. **Database Model Creation** - Create a unified database model
4. **Code Generation** - Generate C# entities from the database model

## Key Components

### Phase 1: SQL Ingestion

- **`ISchemaIngestor`** - Interface for ingesting SQL schema from files
- **`SqlSchemaIngestor`** - Concrete implementation for SQL Server
- **`RawDatabaseSchema`** - Model representing the raw parsed SQL schema

### Phase 2 & 3: Type Resolution and Database Model Creation

- **`ITypeResolver`** - Interface for resolving types
- **`SqlTypeResolver`** - Concrete implementation for SQL Server
- **`DatabaseSchema`** - Model representing the resolved database schema
- **`DatabaseObject`** - Model representing a database object (table or view)
- **`DatabaseColumn`** - Model representing a column in a database object

### Phase 4: Code Generation

- **`ICodeGenerator`** - Interface for generating code
- **`CSharpEntityGenerator`** - Concrete implementation for C# entities
- **`CSharpTypeMapper`** - Helper for mapping database types to C# types

### Orchestration

- **`SqlToEntityGenerator`** - Main orchestrator that ties all phases together

## Benefits of the Refactoring

1. **Separation of Concerns** - Each phase has a clear responsibility
2. **Better Testability** - Each component can be tested in isolation
3. **Flexibility** - Support for different SQL dialects or target languages can be added easily
4. **Maintainability** - Code is more organized and easier to understand

## Migration Strategy

The refactored code provides a clear path for migration. You can gradually move functionality from your existing implementation to the new structure:

1. **Phase 1: SQL Ingestion**
   - The `SqlSchemaIngestor` implements the SQL parsing logic
   - The `RawDatabaseSchema` class stores the raw schema information
   - Focus on making this phase work well before proceeding

2. **Phase 2 & 3: Type Resolution**
   - The `SqlTypeResolver` class handles type resolution
   - The database model classes store the resolved schema
   - Port your existing type resolution logic here

3. **Phase 4: Code Generation**
   - The `CSharpEntityGenerator` generates C# entities
   - Reuse your existing code generation logic here

4. **Testing Each Phase**
   - Test each phase individually with simple examples
   - Ensure the output of each phase is correct before integrating

## Implementation Steps

1. **Organize Your Project**
   - Create the appropriate directories and namespaces
   - Implement the interfaces first

2. **Create the Models**
   - Implement the model classes for each phase
   - Ensure the models capture all necessary information

3. **Implement the Concrete Classes**
   - Implement the concrete classes for each phase
   - Start with the simplest functionality first

4. **Test and Refine**
   - Test each phase with simple examples
   - Refine the implementation based on test results

5. **Integrate the Phases**
   - Integrate all phases in the `SqlToEntityGenerator` class
   - Test the end-to-end process

## Usage Example

```csharp
// Create a logger
var logger = new ConsoleSqlGenLogger();

// Create the generator
var generator = new SqlToEntityGenerator(
    logger,
    "typeMapping.xml",
    debugTargets: "schema.table.column",
    generateNavigationProperties: true);

// Generate entities
var generatedFiles = generator.GenerateEntitiesFromSqlFiles(
    Directory.GetFiles("sql", "*.sql"),
    "output",
    "MyNamespace",
    "MyDatabase");
```

## Future Improvements

- Add support for relationships between tables
- Add support for stored procedures
- Add support for additional SQL dialects
- Add support for generating other languages (e.g., TypeScript, Java) 
