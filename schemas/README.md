# Bravellian Code Generator JSON Schemas

This directory contains JSON Schema files for validating definition files used with the Bravellian Code Generators. These schemas provide validation, IntelliSense, and documentation for all generator types (except DTO/Entity generators).

## Table of Contents

- [Available Schemas](#available-schemas)
  - [FastId Backed Types](#fastid-backed-types)
  - [GUID Backed Types](#guid-backed-types)
  - [String Backed Types](#string-backed-types)
  - [String Backed Enums](#string-backed-enums)
  - [Number Backed Enums](#number-backed-enums)
  - [Multi-Value Backed Types](#multi-value-backed-types)
  - [ERP Capability Definitions](#erp-capability-definitions)
  - [ERP Capabilities Only](#erp-capabilities-only)
  - [ERP Adapter Profile](#erp-adapter-profile)
  - [SQL Code Generation Configuration](#sql-code-generation-configuration)
- [Using the Schemas](#using-the-schemas)
- [Schema Features](#schema-features)

---

## Available Schemas

### FastId Backed Types

**File**: `fastid-backed-type.schema.json`  
**Usage**: For `.fastid.json` files  
**Description**: Generates FastId-backed identifier types. FastId is a high-performance, sortable identifier type that provides time-based ordering and distributed generation capabilities.

**Key Features:**
- Generates strongly-typed identifier classes backed by FastId
- Supports additional properties for metadata
- Provides type-safe identifier handling
- Includes built-in serialization and comparison support

**Example Definition:**

```json
{
  "$schema": "../schemas/fastid-backed-type.schema.json",
  "name": "OrderId",
  "namespace": "Bravellian.Orders",
  "properties": [
    {
      "name": "OriginSystem",
      "type": "string"
    },
    {
      "name": "CreatedAt",
      "type": "DateTime"
    }
  ]
}
```

**Generated Code Features:**
- Immutable value type with FastId backing
- Type-safe equality and comparison operators
- JSON serialization support
- String conversion methods
- Optional metadata properties

---

### GUID Backed Types

**File**: `guid-backed-type.schema.json`  
**Usage**: For `.guid.json` files  
**Description**: Generates GUID-backed identifier types. These provide strongly-typed wrappers around System.Guid with additional metadata capabilities.

**Key Features:**
- Generates strongly-typed identifier classes backed by GUID
- Supports additional properties for metadata
- Provides type-safe identifier handling
- Prevents mixing different identifier types
- Includes built-in serialization and comparison support

**Example Definition:**

```json
{
  "$schema": "../schemas/guid-backed-type.schema.json",
  "name": "CustomerId",
  "namespace": "Bravellian.Customers",
  "properties": [
    {
      "name": "TenantId",
      "type": "string"
    },
    {
      "name": "IsActive",
      "type": "bool"
    }
  ]
}
```

**Generated Code Features:**
- Immutable value type with GUID backing
- Type-safe equality and comparison operators
- JSON serialization support
- String conversion and parsing methods
- NewGuid() factory method
- Optional metadata properties

**Common Use Cases:**
- Entity identifiers (CustomerId, ProductId, OrderId)
- Multi-tenant systems with tenant-scoped IDs
- Distributed systems requiring globally unique identifiers
- Domain-driven design aggregate root identifiers

---

### String Backed Types

**File**: `string-backed-type.schema.json`  
**Usage**: For `.string.json` files  
**Description**: Generates validated string value types with optional regex pattern validation. These provide type-safe wrappers around strings with built-in validation.

**Key Features:**
- Generates strongly-typed string wrapper classes
- Optional regex validation for value constraints
- Supports additional properties for metadata
- Prevents passing raw strings where domain types are expected
- Includes built-in serialization and validation support

**Example Definition:**

```json
{
  "$schema": "../schemas/string-backed-type.schema.json",
  "name": "EmailAddress",
  "namespace": "Bravellian.ValueTypes",
  "validationRegex": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
  "validationRegexConstant": "EmailValidationPattern",
  "properties": [
    {
      "name": "Domain",
      "type": "string"
    },
    {
      "name": "IsVerified",
      "type": "bool"
    }
  ]
}
```

**Generated Code Features:**
- Immutable value type with string backing
- Built-in regex validation (if specified)
- Type-safe equality operators
- JSON serialization support
- String conversion and parsing methods
- Optional metadata properties
- Validation error messages

**Common Use Cases:**
- Email addresses with validation
- Phone numbers with format checking
- SKU/Product codes
- Account numbers
- Custom formatted identifiers (e.g., invoice numbers)
- Domain-specific string values with constraints

---

### String Backed Enums

**File**: `string-backed-enum.schema.json`  
**Usage**: For `.enum.json` and `.string_enum.json` files  
**Description**: Generates string-backed enum types with optional additional properties. Unlike standard C# enums, these provide string serialization and can carry metadata.

**Key Features:**
- Generates enum-like classes with string backing
- Each enum member has a string value
- Optional display names and documentation
- Supports additional properties per enum member
- Type-safe with compile-time checking
- JSON-friendly serialization

**Example Definition:**

```json
{
  "$schema": "../schemas/string-backed-enum.schema.json",
  "name": "AccountingSystemType",
  "namespace": "Bravellian.Accounting",
  "properties": [
    {
      "name": "ApiEndpoint",
      "type": "string"
    },
    {
      "name": "SupportsWebhooks",
      "type": "bool"
    },
    {
      "name": "PriorityLevel",
      "type": "int"
    }
  ],
  "values": {
    "None": {
      "value": "none",
      "documentation": "No accounting system configured"
    },
    "QuickBooks": {
      "value": "quickbooks",
      "display": "QuickBooks Online",
      "documentation": "Intuit QuickBooks Online accounting system"
    },
    "Xero": {
      "value": "xero",
      "display": "Xero Accounting",
      "documentation": "Xero cloud-based accounting platform"
    }
  }
}
```

**Generated Code Features:**
- Static readonly instances for each enum member
- String value property for serialization
- Optional display name property
- Additional custom properties per member
- GetAll() method to enumerate all values
- TryParse() and Parse() methods
- Type-safe equality comparisons
- Implicit string conversion

**Common Use Cases:**
- System integrations (e.g., payment providers, ERP systems)
- Configuration options with metadata
- Status codes with additional information
- Categories or types with display names
- Externally-defined enumerations (API responses)

---

### Number Backed Enums

**File**: `number-backed-enum.schema.json`  
**Usage**: For `.number_enum.json` files  
**Description**: Generates number-backed enum types with configurable underlying numeric types. These extend standard C# enums with additional metadata capabilities.

**Key Features:**
- Generates enum-like classes with numeric backing
- Configurable underlying type (int, long, short, byte, etc.)
- Optional display names and documentation
- Supports additional properties per enum member
- Type-safe with compile-time checking
- Standard numeric enum semantics

**Example Definition:**

```json
{
  "$schema": "../schemas/number-backed-enum.schema.json",
  "name": "PriorityLevel",
  "namespace": "Bravellian.Tasks",
  "type": "int",
  "properties": [
    {
      "name": "Color",
      "type": "string"
    },
    {
      "name": "EscalationThresholdHours",
      "type": "int"
    }
  ],
  "values": {
    "Low": {
      "value": 1,
      "display": "Low Priority",
      "documentation": "Tasks that can be completed when time permits"
    },
    "Medium": {
      "value": 2,
      "display": "Medium Priority",
      "documentation": "Standard priority tasks with normal deadlines"
    },
    "High": {
      "value": 3,
      "display": "High Priority",
      "documentation": "Important tasks requiring prompt attention"
    },
    "Critical": {
      "value": 4,
      "display": "Critical Priority",
      "documentation": "Urgent tasks requiring immediate action"
    }
  }
}
```

**Supported Numeric Types:**
- `int` (default)
- `long`
- `short`
- `byte`
- `uint`
- `ulong`
- `ushort`
- `sbyte`

**Generated Code Features:**
- Numeric value property
- Optional display name property
- Additional custom properties per member
- GetAll() method to enumerate all values
- TryParse() and Parse() methods for numeric values
- Type-safe equality and comparison operators
- Bitwise operations support (for flags)

**Common Use Cases:**
- Priority levels with escalation rules
- Status codes with numeric values
- Severity levels with thresholds
- Permission levels with hierarchy
- API status codes with metadata
- Workflow states with ordering

---

### Multi-Value Backed Types

**File**: `multi-value-backed-type.schema.json`  
**Usage**: For `.multi.json` files  
**Description**: Generates types that can hold multiple predefined string values simultaneously. Similar to flag enums but with string backing and metadata support.

**Key Features:**
- Generates types supporting multiple simultaneous values
- Each value option has a string identifier
- Optional display names and documentation
- Supports additional properties per value
- Type-safe collection of allowed values
- JSON-friendly serialization

**Example Definition:**

```json
{
  "$schema": "../schemas/multi-value-backed-type.schema.json",
  "name": "ContactMethod",
  "namespace": "Bravellian.Communication",
  "properties": [
    {
      "name": "IsPreferred",
      "type": "bool"
    },
    {
      "name": "ResponseTimeHours",
      "type": "int"
    }
  ],
  "values": [
    {
      "name": "Email",
      "value": "email",
      "display": "Email Communication",
      "documentation": "Contact via email address"
    },
    {
      "name": "Phone",
      "value": "phone",
      "display": "Phone Call",
      "documentation": "Contact via telephone"
    },
    {
      "name": "SMS",
      "value": "sms",
      "display": "Text Message",
      "documentation": "Contact via SMS text message"
    },
    {
      "name": "Mail",
      "value": "postal_mail",
      "display": "Postal Mail",
      "documentation": "Contact via postal service"
    }
  ]
}
```

**Generated Code Features:**
- Collection of predefined value instances
- String value property for each option
- Optional display name property
- Additional custom properties per value
- GetAll() method to enumerate all options
- Contains() and Add() methods for managing selections
- Type-safe equality comparisons
- JSON serialization as string array

**Common Use Cases:**
- Communication preferences (email, phone, SMS)
- Feature flags or capabilities
- User permissions or roles
- Notification channels
- Supported payment methods
- Delivery options

---

### ERP Capability Definitions

**File**: `erp-capability-definitions.schema.json`  
**Usage**: For `.erp-capabilities.json` files  
**Description**: Defines ERP capabilities and adapter profiles for ERP integration systems. Generates capability definitions, interfaces, and adapter profile classes.

**Key Features:**
- Defines a catalog of ERP system capabilities
- Generates interface definitions for each capability
- Creates adapter profiles specifying supported capabilities
- Supports multi-system integration scenarios
- Provides compile-time type safety for ERP integrations

**Example Definition:**

```json
{
  "$schema": "../schemas/erp-capability-definitions.schema.json",
  "generatedCapabilitiesNamespace": "Bravellian.ERP.Capabilities",
  "generatedInterfacesNamespace": "Bravellian.ERP.Interfaces",
  "capabilities": [
    {
      "name": "Vendor.CanRead",
      "description": "Ability to read vendor data from the ERP system"
    },
    {
      "name": "Vendor.CanWrite",
      "description": "Ability to create or update vendor records"
    },
    {
      "name": "Invoice.CanRead",
      "description": "Ability to read invoice data from the ERP system"
    },
    {
      "name": "Invoice.CanPost",
      "description": "Ability to post invoices to the ERP system"
    }
  ],
  "adapterProfiles": [
    {
      "name": "QuickBooksAdapter",
      "targetNamespace": "Bravellian.ERP.Adapters",
      "targetClassName": "QuickBooksErpAdapter",
      "supportedCapabilities": [
        "Vendor.CanRead",
        "Vendor.CanWrite",
        "Invoice.CanRead",
        "Invoice.CanPost"
      ]
    },
    {
      "name": "XeroAdapter",
      "targetNamespace": "Bravellian.ERP.Adapters",
      "targetClassName": "XeroErpAdapter",
      "supportedCapabilities": [
        "Vendor.CanRead",
        "Invoice.CanRead"
      ]
    }
  ]
}
```

**Generated Code Components:**

1. **Capability Constants**: Static class with constant strings for each capability
2. **Capability Interfaces**: Interface for each capability (e.g., `IVendorCanRead`)
3. **Adapter Profile Classes**: Classes implementing supported capability interfaces

**Common Use Cases:**
- Multi-ERP system integrations
- Plugin/adapter architecture for accounting systems
- Feature detection in integration scenarios
- Compile-time verification of adapter capabilities
- Dynamic capability discovery

---

### ERP Capabilities Only

**File**: `erp-capabilities-only.schema.json`  
**Usage**: For `.erp-capabilities-only.json` files  
**Description**: Defines only the ERP capability catalog without adapter profiles. Use this when you need to define capabilities separately from their implementations.

**Key Features:**
- Defines a catalog of ERP system capabilities
- Generates capability constants and interfaces
- Does not generate adapter profile classes
- Useful for capability definition libraries
- Allows adapter implementations in separate projects

**Example Definition:**

```json
{
  "$schema": "../schemas/erp-capabilities-only.schema.json",
  "generatedCapabilitiesNamespace": "Bravellian.ERP.Capabilities",
  "generatedInterfacesNamespace": "Bravellian.ERP.Interfaces",
  "capabilities": [
    {
      "name": "Customer.CanRead",
      "description": "Read customer data from ERP"
    },
    {
      "name": "Customer.CanWrite",
      "description": "Create or update customer records"
    },
    {
      "name": "Customer.CanDelete",
      "description": "Delete customer records"
    },
    {
      "name": "Order.CanRead",
      "description": "Read sales order data"
    },
    {
      "name": "Order.CanCreate",
      "description": "Create new sales orders"
    }
  ]
}
```

**Generated Code Components:**

1. **Capability Constants**: Static class with constant strings for each capability
2. **Capability Interfaces**: Interface for each capability

**Use Cases:**
- Shared capability definition libraries
- Capability catalogs referenced by multiple adapters
- Defining standard capabilities across projects
- Separating capability definitions from implementations

---

### ERP Adapter Profile

**File**: `erp-adapter-profile.schema.json`  
**Usage**: For defining individual adapter profiles  
**Description**: Defines a single ERP adapter profile with its supported capabilities. Used in conjunction with capability definitions.

**Example Definition:**

```json
{
  "$schema": "../schemas/erp-adapter-profile.schema.json",
  "name": "SageIntacctAdapter",
  "targetNamespace": "Bravellian.ERP.Adapters.SageIntacct",
  "targetClassName": "SageIntacctErpAdapter",
  "capabilitiesNamespace": "Bravellian.ERP.Capabilities",
  "interfacesNamespace": "Bravellian.ERP.Interfaces",
  "supportedCapabilities": [
    "Vendor.CanRead",
    "Vendor.CanWrite",
    "Customer.CanRead",
    "Customer.CanWrite",
    "Invoice.CanRead",
    "Invoice.CanPost"
  ]
}
```

---

### SQL Code Generation Configuration

**File**: `sql-gen.schema.json`  
**Usage**: For SQL-based C# code generation configuration files  
**Description**: Configuration file for driving the SQL-based C# entity and data access code generator. This enables generating C# classes from SQL DDL definitions.

**Key Features:**
- Configure namespace and DbContext settings
- Define global type mappings from SQL to C# types
- Override table-specific settings (class names, primary keys)
- Define custom read methods
- Configure update behavior per table
- Support for multi-tenant scoping

**Example Definition:**

```json
{
  "$schema": "../schemas/sql-gen.schema.json",
  "namespace": "MyApp.Data.Entities",
  "dbContextName": "MyAppDbContext",
  "dbContextBaseClass": "DbContext",
  "generateDbContext": true,
  "ignoreSchemas": ["sys", "information_schema"],
  "globalTypeMappings": [
    {
      "description": "Map money columns to decimal",
      "priority": 100,
      "match": {
        "sqlType": "money"
      },
      "apply": {
        "csharpType": "decimal"
      }
    },
    {
      "description": "Map timestamp columns to byte array",
      "priority": 100,
      "match": {
        "columnNameRegex": ".*Timestamp$",
        "sqlType": "timestamp"
      },
      "apply": {
        "csharpType": "byte[]"
      }
    }
  ],
  "tables": {
    "dbo.Customer": {
      "csharpClassName": "Customer",
      "primaryKeyOverride": ["CustomerId"],
      "scopeKey": "TenantId",
      "updateConfig": {
        "ignoreColumns": ["CreatedDate", "CreatedBy", "TenantId"]
      },
      "readMethods": [
        {
          "name": "GetByEmail",
          "matchColumns": ["Email"]
        },
        {
          "name": "GetByStatus",
          "matchColumns": ["Status"]
        }
      ],
      "columnOverrides": {
        "CustomerId": {
          "csharpType": "CustomerId"
        },
        "Status": {
          "csharpType": "CustomerStatus"
        }
      }
    }
  }
}
```

**Configuration Sections:**

1. **Global Settings**:
   - `namespace`: Root namespace for generated entities
   - `dbContextName`: Name of the DbContext class
   - `generateDbContext`: Whether to generate DbContext
   - `ignoreSchemas`: Database schemas to skip

2. **Global Type Mappings**:
   - Define reusable SQL-to-C# type mapping rules
   - Use regex patterns for column/table matching
   - Priority-based conflict resolution

3. **Table-Specific Settings**:
   - Override generated class names
   - Define custom primary keys
   - Specify scope keys for multi-tenant scenarios
   - Configure update behavior
   - Define custom read methods
   - Override column types

**Common Use Cases:**
- Generating entity classes from existing databases
- Creating data access layers from SQL DDL
- Type-safe database operations
- Multi-tenant applications with scope keys
- Custom business logic in read/update operations

---

## Using the Schemas

### In VS Code

1. Install the "JSON" or "YAML" extension (built-in support in modern VS Code)
2. Reference the schema in your JSON files using the `$schema` property:

```json
{
  "$schema": "../schemas/fastid-backed-type.schema.json",
  "name": "MyFastId",
  "namespace": "MyNamespace"
}
```

3. VS Code will automatically provide:
   - Real-time validation
   - IntelliSense/auto-completion
   - Hover documentation
   - Error highlighting

### In Other Editors

Most modern editors with JSON support can use these schemas:

1. **JetBrains IDEs (Rider, IntelliJ IDEA)**: Automatically detect `$schema` references
2. **Visual Studio**: Use the JSON Schema Store or reference schemas manually
3. **Sublime Text**: Use LSP-json package
4. **Vim/Neovim**: Use coc-json or ALE with JSON language server

### Command-Line Validation

You can validate JSON files against schemas using tools like:

```bash
# Using ajv-cli
npm install -g ajv-cli
ajv validate -s schemas/fastid-backed-type.schema.json -d my-fastid.json

# Using check-jsonschema
pip install check-jsonschema
check-jsonschema --schemafile schemas/fastid-backed-type.schema.json my-fastid.json
```

---

## Schema Features

All schemas in this directory provide:

### Validation
- **Structure Validation**: Ensures JSON files have the correct structure
- **Required Properties**: Flags missing required fields
- **Type Checking**: Validates property types (string, number, boolean, array, object)
- **Pattern Matching**: Validates strings against regex patterns (e.g., valid C# identifiers)
- **Value Constraints**: Validates enums and numeric ranges

### IntelliSense & Auto-completion
- Property name suggestions as you type
- Enumerated value suggestions
- Nested object structure guidance
- Array item structure templates

### Documentation
- Hover help for all properties showing descriptions
- Usage examples for common scenarios
- Links to related properties and concepts
- Information about defaults and optional fields

### Examples
- Complete working examples embedded in each schema
- Multiple examples showing different feature combinations
- Real-world use case demonstrations

---

## Best Practices

### Naming Conventions
- **Type Names**: Use PascalCase (e.g., `OrderId`, `CustomerStatus`)
- **Namespaces**: Use dotted notation (e.g., `MyCompany.Orders`)
- **Property Names**: Use PascalCase (e.g., `CreatedAt`, `IsActive`)
- **Enum Values**: Use PascalCase for names, lowercase/snake_case for string values

### Organization
- Group related types in the same namespace
- Use separate files for each type definition
- Keep example/test definitions in a separate directory
- Version your schema definitions alongside your code

### Validation
- Always use regex validation for string-backed types with format requirements
- Document validation patterns in the schema
- Provide meaningful error messages
- Test edge cases in validation patterns

### Metadata
- Use the `documentation` property liberally for enum values
- Provide `display` names for user-facing enums
- Include additional properties only when they add clear value
- Keep metadata immutable when possible

---

## Contributing

When adding new generator types or modifying existing ones:

1. Update or create the corresponding schema file
2. Add comprehensive documentation to this README
3. Include complete working examples
4. Update the Table of Contents
5. Test the schema with valid and invalid JSON files
6. Ensure IntelliSense works correctly in VS Code

---

## Support

For issues, questions, or contributions related to these schemas:

1. Check the schema file for inline documentation
2. Review the examples in this README
3. Look at example files in the `/examples` directory
4. Consult the generator source code in `/src/Bravellian.Generators`

---

## Version History

- **v1.0**: Initial comprehensive schema documentation
  - FastId, GUID, String, Enum, Number, Multi-Value types
  - ERP Capability Definitions
  - SQL Code Generation Configuration
