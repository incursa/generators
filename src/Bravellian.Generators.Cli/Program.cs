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

using System.CommandLine;
using Bravellian.Generators.Cli;

var inputOption = new Option<string[]>(
    aliases: ["--input", "-i"],
    description: "One or more input paths (files or directories) containing definition files. Supports multiple values.")
{
    IsRequired = true,
    AllowMultipleArgumentsPerToken = true
};

var outputOption = new Option<DirectoryInfo>(
    aliases: ["--output", "-o"],
    description: "Output directory for generated files")
{
    IsRequired = true
};

var dryRunOption = new Option<bool>(
    aliases: ["--dry-run", "-d"],
    description: "Show what files would be generated without writing them",
    getDefaultValue: () => false);

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable verbose logging",
    getDefaultValue: () => false);

var rootCommand = new RootCommand("Bravellian Code Generator - Generate C# code from definition files")
{
    inputOption,
    outputOption,
    dryRunOption,
    verboseOption
};

rootCommand.SetHandler(async (string[] inputs, DirectoryInfo output, bool dryRun, bool verbose) =>
{
    // Support a single semicolon-separated string as input
    var expandedInputs = new List<string>();
    foreach (var input in inputs)
    {
        if (input.Contains(';'))
        {
            expandedInputs.AddRange(input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        else
        {
            expandedInputs.Add(input);
        }
    }

    var runner = new GeneratorRunner(verbose);
    await runner.RunAsync(expandedInputs.ToArray(), output, dryRun);
}, inputOption, outputOption, dryRunOption, verboseOption);

return await rootCommand.InvokeAsync(args);
