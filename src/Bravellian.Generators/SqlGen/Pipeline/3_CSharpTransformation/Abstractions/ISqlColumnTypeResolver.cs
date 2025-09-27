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

namespace Bravellian.Generators.SqlGen.Pipeline;

using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

internal interface ISqlColumnTypeResolver
{
    string DeriveCSharpTypeFromDbType(string dbType);
    string DeriveDatabaseTypeFromCSharpType(string csharpType);
    string GetCSharpType(SqlDataTypeOption sqlDataType);
    string GetFunctionReturnType(string functionName, FunctionCall functionCall, Dictionary<string, PwTableDefinition> tableDefinitions, Dictionary<string, string> tableAliases, IBvLogger? logger = null);
    string GetSpecializedType(string? databaseName, string tableName, string columnName, string csharpType, string? schemaName = null);
    (string Type, bool? IsNullable) GetSpecializedTypeWithNullability(string? databaseName, string tableName, string columnName, string csharpType, string? schemaName = null);

    List<PwColumnDefinition> ResolveViewColumnTypes(
        CreateViewStatement viewStatement,
        Dictionary<string, PwTableDefinition> tableDefinitions,
        string? databaseName = null,
        Dictionary<string, (string Type, bool? IsNullable)>? dbTypeMappings = null,
        List<CreateViewStatement>? allViewStatements = null);
}

