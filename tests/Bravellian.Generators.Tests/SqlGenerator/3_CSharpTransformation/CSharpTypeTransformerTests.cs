// Copyright (c) Samuel McAravey
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

using Xunit;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
using System.Collections.Generic;
using System.Linq;
// using Bravellian.Generators.SqlGen.Common.TypeMapping; // This namespace doesn't exist

namespace Bravellian.Generators.Tests.SqlGenerator._3_CSharpTransformation
{
    public class CSharpModelTransformerTests
    {
        private readonly TestLogger _logger = new();

        [Fact]
        public void Transform_TypePrecedence_ColumnOverrideWinsOverGlobalMapping()
        {
            // Arrange
            var transformer = new CSharpModelTransformer(_logger, CreateConfigWithTypeMapping(), null);
            var schema = CreateBasicSchema();

            // Add a column override for UserGuid to override it to string instead of Guid
            var config = transformer.Configuration;
            config.Tables["dbo.Users"].ColumnOverrides["UserGuid"] = new ColumnOverride
            {
                CSharpType = "string" // This should override the global mapping that would make it a Guid
            };

            // Act
            var model = transformer.Transform(schema);

            // Assert
            var userClass = model.Classes.First();
            var userGuidProperty = userClass.Properties.First(p => p.Name == "UserGuid");
            Assert.Equal("string", userGuidProperty.Type);
        }

        [Fact]
        public void Transform_GlobalMappingPriority_HighestPriorityWins()
        {
            // Arrange
            var config = new SqlConfiguration();
            
            // Add two conflicting mappings for columns ending with "Amount"
            config.GlobalTypeMappings.Add(new GlobalTypeMapping
            {
                Priority = 100,
                Match = new GlobalTypeMappingMatch { ColumnNameRegex = ".*Amount$" },
                Apply = new GlobalTypeMappingApply { CSharpType = "decimal" }
            });
            
            config.GlobalTypeMappings.Add(new GlobalTypeMapping
            {
                Priority = 200, // Higher priority should win
                Match = new GlobalTypeMappingMatch { ColumnNameRegex = ".*Amount$" },
                Apply = new GlobalTypeMappingApply { CSharpType = "Money" }
            });
            
            var transformer = new CSharpModelTransformer(_logger, config, null);
            var schema = CreateBasicSchema();

            // Act
            var model = transformer.Transform(schema);

            // Assert
            var userClass = model.Classes.First();
            var amountProperty = userClass.Properties.First(p => p.Name == "Amount");
            Assert.Equal("Money", amountProperty.Type); // Higher priority mapping should win
            Assert.True(amountProperty.IsNullable); // Higher priority mapping should win
        }

        [Fact]
        public void Transform_PrimaryKeyOverride_ShouldBeUsedForMethods()
        {
            // Arrange
            var config = new SqlConfiguration();
            config.Tables["dbo.Users"] = new TableConfiguration
            {
                PrimaryKeyOverride = new HashSet<string> { "UserGuid" } // Override PK from Id to UserGuid
            };
            
            var transformer = new CSharpModelTransformer(_logger, config, null);
            var schema = CreateBasicSchema();

            // Act
            var model = transformer.Transform(schema);

            // Assert
            var userClass = model.Classes.First();
            
            // Get method should use UserGuid as parameter
            var getMethod = userClass.Methods.First(m => m.Name == "Get" && m.Type == MethodType.Read);
            Assert.Single(getMethod.Parameters);
            Assert.Equal("userGuid", getMethod.Parameters[0].Name);
            Assert.Equal("UserGuid", getMethod.Parameters[0].SourcePropertyName);
            
            // Delete method should also use UserGuid
            var deleteMethod = userClass.Methods.First(m => m.Name == "Delete" && m.Type == MethodType.Delete);
            Assert.Single(deleteMethod.Parameters);
            Assert.Equal("userGuid", deleteMethod.Parameters[0].Name);
            Assert.Equal("UserGuid", deleteMethod.Parameters[0].SourcePropertyName);
        }

        [Fact]
        public void Transform_ReadMethods_ConfigurationShouldOverrideIndexes()
        {
            // Arrange
            var config = new SqlConfiguration();
            config.Tables["dbo.Users"] = new TableConfiguration
            {
                ReadMethods = new List<ReadMethod>
                {
                    new ReadMethod
                    {
                        Name = "GetByAmountRange",
                        MatchColumns = new List<string> { "Amount" }
                    }
                }
            };
            
            var transformer = new CSharpModelTransformer(_logger, config, null);
            var schema = CreateBasicSchema();
            
            // Add an index to the schema
            var index = new IndexDefinition("IX_Users_TaxAmount", true, false);
            index.ColumnNames.Add("TaxAmount");
            schema.Objects[0].Indexes.Add(index);

            // Act
            var model = transformer.Transform(schema);

            // Assert
            var userClass = model.Classes.First();
            
            // Should have the custom read method from config
            Assert.Contains(userClass.Methods, m => m.Name == "GetByAmountRange");
            
            // Should NOT have an index-based read method for TaxAmount
            Assert.DoesNotContain(userClass.Methods, m => m.Name == "GetByTaxAmount");
        }

        [Fact]
        public void Transform_UpdateMethod_ShouldRespectIgnoreColumns()
        {
            // Arrange
            var config = new SqlConfiguration();
            config.Tables["dbo.Users"] = new TableConfiguration
            {
                UpdateConfig = new UpdateConfig
                {
                    IgnoreColumns = new List<string> { "Amount" }
                }
            };
            
            var transformer = new CSharpModelTransformer(_logger, config, null);
            var schema = CreateBasicSchema();

            // Act
            var model = transformer.Transform(schema);

            // Assert
            var userClass = model.Classes.First();
            var updateMethod = userClass.Methods.First(m => m.Type == MethodType.Update);
            
            // The metadata should contain the ignored columns
            Assert.True(updateMethod.Metadata.ContainsKey("IgnoredColumns"));
            var ignoredColumns = updateMethod.Metadata["IgnoredColumns"] as HashSet<string>;
            Assert.NotNull(ignoredColumns);
            Assert.Contains("Amount", ignoredColumns);
            
            // Primary key column should also be ignored
            Assert.Contains("Id", ignoredColumns);
        }

        private SqlConfiguration CreateConfigWithTypeMapping()
        {
            var config = new SqlConfiguration();
            
            // Add a global mapping for GUID columns
            config.GlobalTypeMappings.Add(new GlobalTypeMapping
            {
                Priority = 100,
                Match = new GlobalTypeMappingMatch { SqlType = ["uniqueidentifier"] },
                Apply = new GlobalTypeMappingApply { CSharpType = "Guid" }
            });
            
            // Add table configuration
            config.Tables["dbo.Users"] = new TableConfiguration();
            
            return config;
        }

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
            ]
            };

            // Set the primary key, which is separate from the column definition
            dbObject.PrimaryKeyColumns.Add("Id");

            return new DatabaseSchema("TestDb")
            {
                Objects = [dbObject]
            };
        }
    }
}
