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

using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators;
using Xunit;
using System.Collections.Generic;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;

namespace Bravellian.Generators.Tests.SqlGenerator._3_CSharpTransformation;

public class CSharpTransformationTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void Transform_WithBasicSchema_ShouldProduceCorrectCSharpModel()
    {
        // Arrange
        var databaseSchema = new DatabaseSchema();

        databaseSchema.AddObject(new DatabaseObject("dbo", "Users", false)
        {
            Columns =
            [
                new DatabaseColumn("Id", PwSqlType.Int, false, true, "dbo", "Users"),
                new DatabaseColumn("Name", PwSqlType.NVarChar, false, false, "dbo", "Users")
            ]
        });

        var config = new SqlConfiguration();
        var transformer = new CSharpModelTransformer(_logger, config, null);

        // Act
        var csharpModel = transformer.Transform(databaseSchema);

        // Assert
        Assert.NotNull(csharpModel);
        Assert.Single(csharpModel.Classes);
        var table = csharpModel.Classes[0];
        Assert.Equal("Users", table.Name);
        Assert.Equal(2, table.Properties.Count);
    }
}

