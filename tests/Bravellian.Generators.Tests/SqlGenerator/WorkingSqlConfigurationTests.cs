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

using Bravellian.Generators.SqlGen.Common.Configuration;
using System.Text.Json;
using Xunit;

namespace Bravellian.Generators.Tests.SqlGenerator;

public class WorkingSqlConfigurationTests
{
    [Fact]
    public void FromJson_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
            {
                "namespace": "MyApp.Data.Entities",
                "generateNavigationProperties": true,
                "generateDbContext": true,
                "globalTypeMappings": [
                    {
                        "description": "Map varchar to string",
                        "match": {
                            "sqlType": ["varchar"]
                        },
                        "apply": {
                            "csharpType": "string"
                        }
                    },
                    {
                        "description": "Map int to int",
                        "match": {
                            "sqlType": ["int"]
                        },
                        "apply": {
                            "csharpType": "int"
                        }
                    }
                ],
                "tables": {
                    "Users": {
                        "csharpClassName": "User"
                    }
                }
            }
            """;

        // Act
        var config = SqlConfiguration.FromJson(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("MyApp.Data.Entities", config.Namespace);
        Assert.True(config.GenerateNavigationProperties);
        Assert.True(config.GenerateDbContext);
        Assert.Equal("BravellianDbContextBase", config.DbContextBaseClass);
        Assert.NotNull(config.GlobalTypeMappings);
        Assert.Equal(2, config.GlobalTypeMappings.Count);
        Assert.NotNull(config.Tables);
        Assert.Single(config.Tables);

        // Check tables dictionary
        Assert.True(config.Tables.ContainsKey("Users"));
        var tableConfig = config.Tables["Users"];
        Assert.Equal("User", tableConfig.CSharpClassName);
    }

    [Fact]
    public void FromJson_WithMinimalConfig_ShouldUseDefaults()
    {
        // Arrange
        var json = """
            {
                "namespace": "MyApp.Data"
            }
            """;

        // Act
        var config = SqlConfiguration.FromJson(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("MyApp.Data", config.Namespace);
        Assert.True(config.GenerateNavigationProperties);
        Assert.True(config.GenerateDbContext);
        Assert.Equal("BravellianDbContextBase", config.DbContextBaseClass);
        Assert.Empty(config.GlobalTypeMappings);
        Assert.Empty(config.Tables);
    }

    [Fact]
    public void FromJson_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var invalidJson = """
            {
                "namespace": "MyApp.Data",
                "invalidProperty": 
            }
            """;

        // Act
        var config = SqlConfiguration.FromJson(invalidJson);

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public void FromJson_WithTypeMappingRules_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
            {
                "namespace": "MyApp.Data",
                "globalTypeMappings": [
                    {
                        "description": "Map varchar columns to string",
                        "match": {
                            "sqlType": ["varchar", "nvarchar"]
                        },
                        "apply": {
                            "csharpType": "string"
                        },
                        "priority": 10
                    },
                    {
                        "description": "Map decimal columns to decimal",
                        "match": {
                            "sqlType": ["decimal", "numeric"]
                        },
                        "apply": {
                            "csharpType": "decimal"
                        },
                        "priority": 20
                    },
                    {
                        "description": "Map uniqueidentifier to Guid",
                        "match": {
                            "sqlType": ["uniqueidentifier"]
                        },
                        "apply": {
                            "csharpType": "Guid"
                        },
                        "priority": 30
                    }
                ]
            }
            """;

        // Act
        var config = SqlConfiguration.FromJson(json);

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.GlobalTypeMappings);
        Assert.Equal(3, config.GlobalTypeMappings.Count);
        
        var varcharMapping = config.GlobalTypeMappings.First(tm => tm.Description == "Map varchar columns to string");
        Assert.Contains("varchar", varcharMapping.Match.SqlType);
        Assert.Contains("nvarchar", varcharMapping.Match.SqlType);
        Assert.Equal("string", varcharMapping.Apply.CSharpType);
        Assert.Equal(10, varcharMapping.Priority);
        
        var decimalMapping = config.GlobalTypeMappings.First(tm => tm.Description == "Map decimal columns to decimal");
        Assert.Contains("decimal", decimalMapping.Match.SqlType);
        Assert.Contains("numeric", decimalMapping.Match.SqlType);
        Assert.Equal("decimal", decimalMapping.Apply.CSharpType);
        Assert.Equal(20, decimalMapping.Priority);
        
        var guidMapping = config.GlobalTypeMappings.First(tm => tm.Description == "Map uniqueidentifier to Guid");
        Assert.Contains("uniqueidentifier", guidMapping.Match.SqlType);
        Assert.Equal("Guid", guidMapping.Apply.CSharpType);
        Assert.Equal(30, guidMapping.Priority);
    }

    [Fact]
    public void FromJson_WithTableConfiguration_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
            {
                "namespace": "MyApp.Data",
                "tables": {
                    "dbo.Users": {
                        "description": "Users table",
                        "csharpClassName": "User",
                        "primaryKeyOverride": ["Id"],
                        "updateConfig": {
                            "ignoreColumns": ["CreatedDate", "CreatedBy"]
                        },
                        "columnOverrides": {
                            "Email": {
                                "description": "User's email address",
                                "csharpType": "EmailAddress",
                                "sqlType": "nvarchar(255)",
                                "isNullable": false
                            },
                            "UserType": {
                                "csharpType": "UserTypes",
                                "isNullable": false
                            }
                        }
                    },
                    "dbo.Orders": {
                        "csharpClassName": "CustomerOrder",
                        "readMethods": [
                            {
                                "name": "GetByStatusAndDate",
                                "matchColumns": ["Status", "OrderDate", "CustomerId"]
                            }
                        ]
                    }
                }
            }
            """;

        // Act
        var config = SqlConfiguration.FromJson(json);

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.Tables);
        Assert.Equal(2, config.Tables.Count);
        
        // Check Users table config
        Assert.True(config.Tables.ContainsKey("dbo.Users"));
        var usersTable = config.Tables["dbo.Users"];
        Assert.Equal("Users table", usersTable.Description);
        Assert.Equal("User", usersTable.CSharpClassName);
        Assert.NotNull(usersTable.PrimaryKeyOverride);
        Assert.Single(usersTable.PrimaryKeyOverride);
        Assert.Equal("Id", usersTable.PrimaryKeyOverride.First());
        
        Assert.NotNull(usersTable.UpdateConfig);
        Assert.NotNull(usersTable.UpdateConfig.IgnoreColumns);
        Assert.Equal(2, usersTable.UpdateConfig.IgnoreColumns!.Count);
        Assert.Contains("CreatedDate", usersTable.UpdateConfig.IgnoreColumns!);
        Assert.Contains("CreatedBy", usersTable.UpdateConfig.IgnoreColumns!);
        
        Assert.NotNull(usersTable.ColumnOverrides);
        Assert.Equal(2, usersTable.ColumnOverrides.Count);
        
        Assert.True(usersTable.ColumnOverrides.ContainsKey("Email"));
        var emailColumn = usersTable.ColumnOverrides["Email"];
        Assert.Equal("User's email address", emailColumn.Description);
        Assert.Equal("EmailAddress", emailColumn.CSharpType);
        Assert.Equal("nvarchar(255)", emailColumn.SqlType);
        Assert.False(emailColumn.IsNullable);
        
        Assert.True(usersTable.ColumnOverrides.ContainsKey("UserType"));
        var userTypeColumn = usersTable.ColumnOverrides["UserType"];
        Assert.Equal("UserTypes", userTypeColumn.CSharpType);
        Assert.False(userTypeColumn.IsNullable);
        
        // Check Orders table config
        Assert.True(config.Tables.ContainsKey("dbo.Orders"));
        var ordersTable = config.Tables["dbo.Orders"];
        Assert.Equal("CustomerOrder", ordersTable.CSharpClassName);
        
        Assert.NotNull(ordersTable.ReadMethods);
        Assert.Single(ordersTable.ReadMethods);
        var readMethod = ordersTable.ReadMethods[0];
        Assert.Equal("GetByStatusAndDate", readMethod.Name);
        Assert.NotNull(readMethod.MatchColumns);
        Assert.Equal(3, readMethod.MatchColumns.Count);
        Assert.Contains("Status", readMethod.MatchColumns);
        Assert.Contains("OrderDate", readMethod.MatchColumns);
        Assert.Contains("CustomerId", readMethod.MatchColumns);
    }
}

