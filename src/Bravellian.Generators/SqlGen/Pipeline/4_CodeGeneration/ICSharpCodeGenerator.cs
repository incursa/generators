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

using Bravellian.Generators.SqlGen.Pipeline._3_CSharpTransformation.Models;
using System.Collections.Generic;

namespace Bravellian.Generators.SqlGen.Pipeline._4_CodeGeneration
{
    /// <summary>
    /// Interface for Phase 4 code generators that render C# source files from the final model.
    /// </summary>
    public interface ICSharpCodeGenerator
    {
        /// <summary>
        /// Generates C# source file contents from the final C#-ready model.
        /// </summary>
        /// <param name="generationModel">The final model from Phase 3.</param>
        /// <returns>A dictionary where keys are file names and values are the generated C# code contents.</returns>
        Dictionary<string, string> Generate(GenerationModel generationModel);

        /// <summary>
        /// Writes the generated files to disk.
        /// </summary>
        /// <param name="generatedFiles">Dictionary of file names and their contents.</param>
        void WriteFilesToDisk(Dictionary<string, string> generatedFiles, string outputDirectory);
    }
}
