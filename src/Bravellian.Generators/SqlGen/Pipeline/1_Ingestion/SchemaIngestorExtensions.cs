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

using System.Collections.Generic;
using System.IO;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;

namespace Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;

public static class SchemaIngestorExtensions
{
    public static RawDatabaseSchema IngestSchemaFromFiles(this ISchemaIngestor ingestor,
        IEnumerable<string> sqlFilePaths,
        string? databaseName = null)
    {
        var sqlScriptText = new List<string>();
        foreach (var filePath in sqlFilePaths)
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException($"SQL file not found: {filePath}");
            }
            sqlScriptText.Add(System.IO.File.ReadAllText(filePath));
        }

        return ingestor.Ingest(sqlScriptText);
    }
}
