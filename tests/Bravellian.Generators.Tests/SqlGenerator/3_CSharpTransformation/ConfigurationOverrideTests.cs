// Copyright (c) Bravellian
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using Bravellian.Generators;
using Bravellian.Generators.SqlGen.Common;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator._3_CSharpTransformation;

public class ConfigurationOverrideTests
{
    private readonly TestLogger logger = new ();

    private DatabaseSchema CreateBasicSchema()
    {
        var dbObject = new DatabaseObject("dbo", "Users", false)
        {
            Columns =
            [

                // This column is NOT nullable
                new DatabaseColumn("Id", PwSqlType.Int, false, true, "dbo", "Users"),

                // This column is NOT nullable
                new DatabaseColumn("UserGuid", PwSqlType.UniqueIdentifier, false, false, "dbo", "Users"),

                // This column IS nullable
                new DatabaseColumn("Amount", PwSqlType.Decimal, true, false, "dbo", "Users"),

                // This column is NOT nullable
                new DatabaseColumn("TaxAmount", PwSqlType.Decimal, false, false, "dbo", "Users"),
            ],
        };

        // Set the primary key, which is separate from the column definition
        dbObject.PrimaryKeyColumns.Add("Id");

        return new DatabaseSchema("TestDb")
        {
            Objects = [dbObject],
        };
    }

    [Fact]
    public void Transform_WithTableClassNameOverride_ShouldChangeClassName()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration
        {
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Users"] = new () { CSharpClassName = "AppUser" },
            },
        };
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        Assert.Single(csharpModel.Classes);
        Assert.Equal("AppUser", csharpModel.Classes[0].Name);
    }

    [Fact]
    public void Transform_WithColumnOverrideCSharpType_ShouldTakeHighestPrecedence()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration
        {
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Users"] = new ()
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>(
StringComparer.Ordinal)
                    {
                        ["Id"] = new () { CSharpType = "CustomId" }, // Ultimate override
                    },
                },
            },
            GlobalTypeMappings =
            [

                // This should be ignored for the Id column due to the column-specific override
                new ()
                {
                    Priority = 100,
                    Match = new () { SqlType = ["int"] },
                    Apply = new () { CSharpType = "int" },
                }

            ],
        };
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        var userClass = csharpModel.Classes[0];
        var idProperty = userClass.Properties.Find(p => string.Equals(p.Name, "Id", StringComparison.Ordinal));
        Assert.NotNull(idProperty);
        Assert.Equal("CustomId", idProperty.Type);
    }

    [Fact]
    public void Transform_WithGlobalTypeMapping_ShouldApplyWithPriority()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration
        {
            GlobalTypeMappings =
            [

                // Low priority rule for any column ending in 'Amount'
                new ()
                {
                    Priority = 10,
                    Match = new () { ColumnNameRegex = "Amount$" },
                    Apply = new () { CSharpType = "Money" },
                },

                // High priority rule for a specific SQL type
                new ()
                {
                    Priority = 100,
                    Match = new () { SqlType = ["decimal"] },
                    Apply = new () { CSharpType = "PreciseDecimal" },
                }

            ],
        };
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        var userClass = csharpModel.Classes[0];
        var amountProperty = userClass.Properties.Find(p => string.Equals(p.Name, "Amount", StringComparison.Ordinal));
        var taxAmountProperty = userClass.Properties.Find(p => string.Equals(p.Name, "TaxAmount", StringComparison.Ordinal));

        // Both decimal columns should be mapped to 'PreciseDecimal' because that rule has higher priority
        Assert.NotNull(amountProperty);
        Assert.Equal("PreciseDecimal", amountProperty.Type); // Nullable because the schema says it is
        Assert.True(amountProperty.IsNullable); // Nullable because the schema says it is

        Assert.NotNull(taxAmountProperty);
        Assert.Equal("PreciseDecimal", taxAmountProperty.Type); // Not nullable
    }

    [Fact]
    public void Transform_WithConflictingOverrides_ColumnOverrideShouldWin()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration
        {
            GlobalTypeMappings =
            [

                // Global rule for all uniqueidentifiers
                new ()
                {
                    Priority = 50,
                    Match = new () { SqlType = ["uniqueidentifier"] },
                    Apply = new () { CSharpType = "System.Guid" },
                }

            ],
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Users"] = new ()
                {
                    ColumnOverrides = new Dictionary<string, ColumnOverride>(
StringComparer.Ordinal)
                    {
                        // Specific override for this one column
                        ["UserGuid"] = new () { CSharpType = "StronglyTypedGuid" }
                    },
                },
            },
        };
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        var userClass = csharpModel.Classes[0];
        var guidProperty = userClass.Properties.Find(p => string.Equals(p.Name, "UserGuid", StringComparison.Ordinal));

        // The column-specific override should win over the global mapping
        Assert.NotNull(guidProperty);
        Assert.Equal("StronglyTypedGuid", guidProperty.Type);
    }

    [Fact]
    public void Transform_WithNoOverrides_ShouldUseDefaultConventions()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration(); // Empty config
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        var userClass = csharpModel.Classes[0];
        var idProperty = userClass.Properties.Find(p => string.Equals(p.Name, "Id", StringComparison.Ordinal));
        var amountProperty = userClass.Properties.Find(p => string.Equals(p.Name, "Amount", StringComparison.Ordinal));

        Assert.NotNull(idProperty);
        Assert.Equal("int", idProperty.Type);
        Assert.True(idProperty.IsPrimaryKey); // Should be true because we set it in CreateBasicSchema

        Assert.NotNull(amountProperty);
        Assert.Equal("decimal", amountProperty.Type);
        Assert.True(amountProperty.IsNullable);
    }

    [Fact]
    public void Transform_WithPrimaryKeyOverride_ShouldGenerateCorrectMethods()
    {
        // Arrange
        var schema = this.CreateBasicSchema();
        var config = new SqlConfiguration
        {
            Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
            {
                ["dbo.Users"] = new ()
                {
                    // Override the PK from 'Id' to 'UserGuid' for data access methods
                    PrimaryKeyOverride = ["UserGuid"],
                },
            },
        };
        var transformer = new CSharpModelTransformer(this.logger, config, null);

        // Act
        var csharpModel = transformer.Transform(schema);

        // Assert
        var userClass = csharpModel.Classes[0];
        var getMethod = userClass.Methods.Find(m => string.Equals(m.Name, "Get", StringComparison.Ordinal));
        var deleteMethod = userClass.Methods.Find(m => string.Equals(m.Name, "Delete", StringComparison.Ordinal));

        // The 'Id' property should still be marked as the PK on the model itself
        Assert.False(userClass.Properties.Find(p => string.Equals(p.Name, "Id", StringComparison.Ordinal))?.IsPrimaryKey);
        Assert.True(userClass.Properties.Find(p => string.Equals(p.Name, "UserGuid", StringComparison.Ordinal))?.IsPrimaryKey);

        // But the Get and Delete methods should use the overridden key
        Assert.NotNull(getMethod);
        Assert.Single(getMethod.Parameters);
        Assert.Equal("userGuid", getMethod.Parameters[0].Name);
        Assert.Equal("Guid", getMethod.Parameters[0].Type); // Default mapping for uniqueidentifier

        Assert.NotNull(deleteMethod);
        Assert.Single(deleteMethod.Parameters);
        Assert.Equal("userGuid", deleteMethod.Parameters[0].Name);
    }
}
