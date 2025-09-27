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

namespace Bravellian.Generators.SqlGen.Pipeline.1_Ingestion
{
    using System.Collections;
    using System.Collections.Generic;
    using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;

    public interface ISchemaIngestor
{
    /// <summary>
    /// Ingests SQL statements and builds a raw database schema.
    /// </summary>
    /// <param name="sqlStatements">Array of SQL DDL statements to parse.</param>
    /// <returns>A raw database schema representing the SQL objects.</returns>
    RawDatabaseSchema Ingest(IEnumerable<string> sqlStatements);
}
}
