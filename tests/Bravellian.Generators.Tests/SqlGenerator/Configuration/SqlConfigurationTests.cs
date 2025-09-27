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

namespace Bravellian.Generators.Tests.SqlGenerator.Configuration
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Bravellian.Generators.SqlGen.Common.Configuration;
    using Xunit;

    public class SqlConfigurationTests
    {
        [Fact]
        public void FromJson_WithCompleteConfiguration_ShouldDeserializeAllProperties()
        {
            // Arrange
            var json = @"{
                ""namespace"": ""Custom.Entities"",
                ""generateNavigationProperties"": false,
                ""generateDbContext"": true,
                ""dbContextBaseClass"": ""CustomDbContext"",
                ""globalTypeMappings"": [
                    {
                        ""description"": ""Map amount columns to Money"",
                        ""priority"": 50,
                        ""match"": {
                            ""columnNameRegex"": "".*Amount$"",
                            ""sqlType"": ""decimal""
                        },
                        ""apply"": {
                            ""csharpType"": ""Money""
                        }
                    }
                ],
                ""tables"": {
                    ""dbo.Customer"": {
                        ""description"": ""Customer table"",
                        ""csharpClassName"": ""CustomerEntity"",
                        ""primaryKeyOverride"": [""CustomerId""],
                        ""updateConfig"": {
                            ""ignoreColumns"": [""CreatedDate"", ""TenantId""]
                        },
                        ""readMethods"": [
                            {
                                ""name"": ""GetByEmailAndStatus"",
                                ""matchColumns"": [""Email"", ""Status""]
                            }
                        ],
                        ""columnOverrides"": {
                            ""Status"": {
                                ""description"": ""Status column"",
                                ""sqlType"": ""nvarchar(20)"",
                                ""isNullable"": false,
                                ""csharpType"": ""CustomerStatus""
                            }
                        }
                    }
                }
            }";

            // Act
            var config = SqlConfiguration.FromJson(json);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("Custom.Entities", config.Namespace);
            Assert.False(config.GenerateNavigationProperties);
            Assert.True(config.GenerateDbContext);
            Assert.Equal("CustomDbContext", config.DbContextBaseClass);

            // Global Type Mappings
            Assert.Single(config.GlobalTypeMappings);
            var mapping = config.GlobalTypeMappings[0];
            Assert.Equal("Map amount columns to Money", mapping.Description);
            Assert.Equal(50, mapping.Priority);
            Assert.Equal(".*Amount$", mapping.Match.ColumnNameRegex);
            Assert.Single(mapping.Match.SqlType);
            Assert.Equal("decimal", mapping.Match.SqlType[0]);
            Assert.Equal("Money", mapping.Apply.CSharpType);

            // Table Configuration
            Assert.Single(config.Tables);
            Assert.True(config.Tables.ContainsKey("dbo.Customer"));
            var tableConfig = config.Tables["dbo.Customer"];
            Assert.Equal("Customer table", tableConfig.Description);
            Assert.Equal("CustomerEntity", tableConfig.CSharpClassName);
            Assert.Single(tableConfig.PrimaryKeyOverride);
            Assert.Contains("CustomerId", tableConfig.PrimaryKeyOverride);

            // Update Config
            Assert.NotNull(tableConfig.UpdateConfig);
            Assert.Equal(2, tableConfig.UpdateConfig.IgnoreColumns.Count);
            Assert.Contains("CreatedDate", tableConfig.UpdateConfig.IgnoreColumns);
            Assert.Contains("TenantId", tableConfig.UpdateConfig.IgnoreColumns);

            // Read Methods
            Assert.Single(tableConfig.ReadMethods);
            var readMethod = tableConfig.ReadMethods[0];
            Assert.Equal("GetByEmailAndStatus", readMethod.Name);
            Assert.Equal(2, readMethod.MatchColumns.Count);
            Assert.Contains("Email", readMethod.MatchColumns);
            Assert.Contains("Status", readMethod.MatchColumns);

            // Column Overrides
            Assert.Single(tableConfig.ColumnOverrides);
            Assert.True(tableConfig.ColumnOverrides.ContainsKey("Status"));
            var columnOverride = tableConfig.ColumnOverrides["Status"];
            Assert.Equal("Status column", columnOverride.Description);
            Assert.Equal("nvarchar(20)", columnOverride.SqlType);
            Assert.False(columnOverride.IsNullable);
            Assert.Equal("CustomerStatus", columnOverride.CSharpType);

            // GetColumnOverride helper method
            var retrievedOverride = config.GetColumnOverride("dbo", "Customer", "Status");
            Assert.NotNull(retrievedOverride);
            Assert.Equal("CustomerStatus", retrievedOverride.CSharpType);
        }

        [Fact]
        public void FromJson_WithInvalidJson_ShouldReturnNull()
        {
            // Arrange
            var json = "{ invalid json";

            // Act
            var config = SqlConfiguration.FromJson(json);

            // Assert
            Assert.Null(config);
        }

        [Fact]
        public void FromJson_WithStringOrArrayValues_ShouldHandleBothFormats()
        {
            // Arrange
            var jsonWithStringValue = @"{
                ""globalTypeMappings"": [
                    {
                        ""match"": {
                            ""sqlType"": ""varchar""
                        },
                        ""apply"": {
                            ""csharpType"": ""string""
                        }
                    }
                ]
            }";

            var jsonWithArrayValue = @"{
                ""globalTypeMappings"": [
                    {
                        ""match"": {
                            ""sqlType"": [""varchar"", ""nvarchar"", ""char""]
                        },
                        ""apply"": {
                            ""csharpType"": ""string""
                        }
                    }
                ]
            }";

            // Act
            var configWithString = SqlConfiguration.FromJson(jsonWithStringValue);
            var configWithArray = SqlConfiguration.FromJson(jsonWithArrayValue);

            // Assert
            Assert.NotNull(configWithString);
            Assert.NotNull(configWithArray);

            Assert.Single(configWithString.GlobalTypeMappings[0].Match.SqlType);
            Assert.Equal("varchar", configWithString.GlobalTypeMappings[0].Match.SqlType[0]);

            Assert.Equal(3, configWithArray.GlobalTypeMappings[0].Match.SqlType.Count);
            Assert.Equal("varchar", configWithArray.GlobalTypeMappings[0].Match.SqlType[0]);
            Assert.Equal("nvarchar", configWithArray.GlobalTypeMappings[0].Match.SqlType[1]);
            Assert.Equal("char", configWithArray.GlobalTypeMappings[0].Match.SqlType[2]);
        }

        [Fact]
        public void GetColumnOverride_WithNoMatchingTable_ShouldReturnNull()
        {
            // Arrange
            var config = new SqlConfiguration
            {
                Tables = new Dictionary<string, TableConfiguration>(
StringComparer.Ordinal)
                {
                    { "dbo.Customer", new TableConfiguration() },
                },
            };

            // Act
            var result = config.GetColumnOverride("dbo", "Product", "Id");

            // Assert
            Assert.Null(result);
        }
    }
}
