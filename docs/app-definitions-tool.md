# App Definitions Tool

## Why This Exists

`Incursa.Generators.Tool` is for deterministic code generation that should not happen implicitly during compilation.

Use it when:

- definitions live outside the consuming project
- generated output must land in specific directories or projects
- CI needs a `--check` mode for stale output detection
- local development should use an explicit command instead of hidden build hooks

Prefer the Roslyn package when generation is project-local and tightly coupled to compilation. Prefer this tool when generation is cross-project, repo-structured, or needs explicit write/check semantics.

## Current Scenario

V1 supports page-feature generation around a UI engine seam:

- page contract models
- UI engine interfaces
- Razor `PageModel` adapter bases
- DI registration helpers

It does not generate:

- UI engine implementation classes
- placeholder test classes
- repo-specific framework scaffolding

## High-Level Architecture

The tool uses a two-stage pipeline:

1. Parse and validate
   - load config
   - discover XML definitions
   - parse to a canonical model
   - validate names, duplicates, references, and shape rules
2. Emit and synchronize
   - run target-specific emitters
   - build deterministic file content and paths
   - either write files or check for drift

The reusable pieces live in [`src/Incursa.Generators.AppDefinitions`](/C:/src/incursa/generators/src/Incursa.Generators.AppDefinitions). The CLI lives in [`src/Incursa.Generators.Tool`](/C:/src/incursa/generators/src/Incursa.Generators.Tool).

## Config Shape

Config is JSON and versioned. Paths are resolved relative to the config file.

```json
{
  "version": 1,
  "definitionRoot": "defs",
  "definitionPatterns": ["*.page.xml"],
  "validation": {
    "knownTypeNames": ["IReadOnlyList"]
  },
  "targets": {
    "contracts": {
      "kind": "page-contract-models",
      "directory": "../src/MyApp.Contracts/Generated/Pages",
      "namespace": "MyApp.Contracts.Pages"
    },
    "uiEngines": {
      "kind": "page-ui-engine-interface",
      "directory": "../src/MyApp.Server/Generated/UiEngines",
      "namespace": "MyApp.Server.UiEngines",
      "imports": {
        "contracts": "MyApp.Contracts.Pages"
      }
    },
    "pageModelBases": {
      "kind": "page-model-base",
      "directory": "../src/MyApp.Web/Generated/PageModels",
      "namespace": "MyApp.Web.Pages.Base",
      "imports": {
        "contracts": "MyApp.Contracts.Pages",
        "uiEngines": "MyApp.Server.UiEngines"
      }
    },
    "registrations": {
      "kind": "page-registration-helper",
      "directory": "../src/MyApp.Server/Generated",
      "namespace": "MyApp.Server.Generated",
      "imports": {
        "uiEngines": "MyApp.Server.UiEngines"
      }
    }
  }
}
```

Supported target kinds in this first pass:

- `page-contract-models`
- `page-ui-engine-interface`
- `page-model-base`
- `page-registration-helper`

## Commands

Build the tool:

```powershell
dotnet build C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj
```

Pack the tool locally:

```powershell
dotnet pack C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -c Debug -o C:\src\incursa\generators\nupkgs-test
```

Install the packed tool to a local tool path:

```powershell
dotnet tool install Incursa.Generators.Tool --tool-path C:\src\incursa\generators\.tools --add-source C:\src\incursa\generators\nupkgs-test
```

Update the local install:

```powershell
dotnet tool update Incursa.Generators.Tool --tool-path C:\src\incursa\generators\.tools --add-source C:\src\incursa\generators\nupkgs-test
```

Uninstall the local install:

```powershell
dotnet tool uninstall Incursa.Generators.Tool --tool-path C:\src\incursa\generators\.tools
```

Validate definitions:

```powershell
dotnet run --project C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -- validate --config C:\src\incursa\generators\examples\AppDefinitions\app-definitions.json
```

Write generated outputs:

```powershell
dotnet run --project C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -- generate --config C:\src\incursa\generators\examples\AppDefinitions\app-definitions.json --write
```

Check for drift in CI:

```powershell
dotnet run --project C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -- generate --config C:\src\incursa\generators\examples\AppDefinitions\app-definitions.json --check
```

Optional flags:

- `--definitions <path>` overrides `definitionRoot`
- `--filter <pattern>` narrows generation to matching features
- `--verbosity quiet|normal|detailed`

## Ownership And Orphan Cleanup

Every generated code file contains a deterministic ownership header:

- tool name
- command name
- `do not edit by hand` notice
- target name
- target kind
- relative output path

For unfiltered runs, each configured target also writes a per-target manifest file in the target directory:

- `.incursa-appdefs.<target-name>.manifest.json`

The manifest is the primary source of truth for orphan cleanup. During unfiltered runs:

- `generate --check` fails if a previously owned generated file is no longer expected
- `generate --write` removes previously owned generated files that are no longer expected
- non-owned files are never deleted, even if they sit beside generated files
- if a file was once managed but no longer carries a matching ownership header, the tool fails and requires manual cleanup

There is also an ownership-header scan fallback inside configured target directories so older generated files from pre-manifest runs can still be detected as stale.

## Safety Limits

- The tool only deletes files it can prove it owns.
- It never deletes handwritten files that do not carry matching ownership metadata.
- Filtered runs are intentionally conservative:
  - `--filter` skips orphan cleanup
  - `--filter` skips manifest rewrites
  - use unfiltered `--check` or `--write` for full-repo reconciliation
- If a target is removed from config entirely, the tool no longer manages that directory. Clean it explicitly.

## Sample Files

Sample source inputs live here:

- [`examples/AppDefinitions/app-definitions.json`](/C:/src/incursa/generators/examples/AppDefinitions/app-definitions.json)
- [`examples/AppDefinitions/defs/Customer/CustomerList.page.xml`](/C:/src/incursa/generators/examples/AppDefinitions/defs/Customer/CustomerList.page.xml)

## Adding Another Emitter Later

To add another deterministic generator scenario:

1. Add a new emitter implementing `IGenerationTargetEmitter`.
2. Register its `kind` in [`AppDefinitionGenerator`](/C:/src/incursa/generators/src/Incursa.Generators.AppDefinitions/Pipeline/AppDefinitionGenerator.cs).
3. Extend validation if the new emitter depends on additional shape rules.
4. Add sample config and golden-file tests for the new target kind.

Keep new emitters target-specific. The reusable seams are already separated into models, parser, validation, emitters, and synchronization.
