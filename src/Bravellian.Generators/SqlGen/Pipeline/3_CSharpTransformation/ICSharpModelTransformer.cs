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

namespace Bravellian.Generators.SqlGen.Pipeline.3_CSharpTransformation
{
    using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;
    using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;

    public interface ICSharpModelTransformer
{
    /// <summary>
    /// Transforms the refined database schema into a C# generation model.
    /// This is Phase 3 of the pipeline, where SQL types are mapped to C# types
    /// and the data access methods are generated based on the schema and configuration.
    /// </summary>
    /// <param name="databaseSchema">The refined database schema from Phase 2.</param>
    /// <returns>A C#-ready model containing all information needed for code generation.</returns>
    GenerationModel Transform(DatabaseSchema databaseSchema);
}
}
