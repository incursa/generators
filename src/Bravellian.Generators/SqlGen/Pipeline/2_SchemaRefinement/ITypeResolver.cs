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

using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Common.Configuration;

namespace Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement
{
    public interface ITypeResolver
    {
        /// <summary>
        /// Resolves SQL types to C# types based on configuration rules
        /// </summary>
        /// <param name="databaseModel">The raw database model from the ingestion phase</param>
        /// <param name="configuration">Optional configuration with type mapping rules</param>
        /// <returns>The refined database model with resolved types</returns>
        RawDatabaseSchema Resolve(RawDatabaseSchema databaseModel, SqlConfiguration configuration = null);
    }
}
