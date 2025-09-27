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

namespace Bravellian.Generators.SqlGen.Pipeline.2_SchemaRefinement
{
    using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
    using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement.Model;

/// <summary>
/// Defines the contract for the schema refinement phase of the pipeline.
/// </summary>
    public interface ISchemaRefiner
{
    /// <summary>
    /// Refines the raw database model by applying configuration overrides.
    /// </summary>
    /// <param name="rawSchema">The raw database model from the ingestion phase.</param>
    /// <returns>A refined and completed database schema model.</returns>
    DatabaseSchema Refine(RawDatabaseSchema rawSchema);
}
}
