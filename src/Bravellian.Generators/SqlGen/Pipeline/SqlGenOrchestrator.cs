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
using Bravellian.Generators.SqlGen.Common.Configuration;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;
using Bravellian.Generators.SqlGen.Pipeline._2_SchemaRefinement;
using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation;
using Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration;

namespace Bravellian.Generators.SqlGen.Pipeline
{
    public class SqlGenOrchestrator
    {
        private readonly ISchemaIngestor _schemaIngestor;
        private readonly ISchemaRefiner _schemaRefiner;
        private readonly ICSharpModelTransformer _cSharpModelTransformer;
        private readonly ICSharpCodeGenerator _cSharpCodeGenerator;
        private readonly IBvLogger _logger;
        private readonly SqlConfiguration _configuration;

        public SqlGenOrchestrator(
            ISchemaIngestor schemaIngestor,
            ISchemaRefiner schemaRefiner,
            ICSharpModelTransformer cSharpModelTransformer,
            ICSharpCodeGenerator cSharpCodeGenerator,
            SqlConfiguration configuration,
            IBvLogger logger)
        {
            _schemaIngestor = schemaIngestor;
            _schemaRefiner = schemaRefiner;
            _cSharpModelTransformer = cSharpModelTransformer;
            _cSharpCodeGenerator = cSharpCodeGenerator;
            _configuration = configuration;
            _logger = logger;
        }

        public IReadOnlyDictionary<string, string> Generate(string[] sqlFiles)
        {
            _logger.LogMessage("Phase 1: Ingesting SQL schema...");
            var rawDatabaseModel = _schemaIngestor.Ingest(sqlFiles);
            
            _logger.LogMessage("Phase 2: Refining schema model...");
            var refinedSchema = _schemaRefiner.Refine(rawDatabaseModel);
            
            _logger.LogMessage("Phase 3: Transforming to C# model...");
            var csharpModel = _cSharpModelTransformer.Transform(refinedSchema);
            
            _logger.LogMessage("Phase 4: Generating C# code...");
            var generatedCode = _cSharpCodeGenerator.Generate(csharpModel);
            
            _logger.LogMessage("Code generation complete.");
            return generatedCode;
        }
    }
}
