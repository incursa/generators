# App Definitions Tool Design

## Why This Exists

The existing `Incursa.Generators` package is a Roslyn source generator. That model works well for project-local type generation, but it is a poor fit for cross-project page feature outputs that need explicit destinations, stable pathing, and CI-friendly drift detection.

This design introduces a deterministic CLI tool for XML definition driven generation. The first concrete scenario is page-feature generation around the UI engine pattern, but the architecture is intentionally staged so additional deterministic generators can be added later without rebuilding the CLI shape.

## Goals

- Parse XML definition files into a canonical model.
- Validate definitions with clear, file-aware diagnostics.
- Emit deterministic artifacts into config-driven output targets.
- Support explicit `generate` and `validate` commands plus `--write` and `--check`.
- Keep parser, validation, emission, and file update logic reusable.

## Non-Goals For V1

- Roslyn source generation.
- Hidden build integration.
- Repo-specific paths or namespaces.
- Generated UI engine implementation classes.
- Generated placeholder tests.
- A generalized plugin system.

## Proposed Project Shape

- `src/Incursa.Generators.AppDefinitions`
  - Canonical models
  - Config loading
  - XML parsing
  - Validation
  - Emitters
  - Deterministic write/check pipeline
- `src/Incursa.Generators.Tool`
  - CLI command parsing
  - Console/reporting orchestration
  - Exit codes

Tests should cover parser, validation, emitters, end-to-end generation, and check mode drift detection.

## Pipeline

### Stage 1: Parse And Validate

1. Load JSON config.
2. Discover definition files from configured roots.
3. Parse XML into a canonical model with source locations where practical.
4. Normalize stable conventions required by the first emitters.
5. Validate required attributes, duplicates, invalid references, and emitter-specific shape rules.

### Stage 2: Emit

1. Select configured output targets by `kind`.
2. Emit target-specific artifacts from the canonical model.
3. Sort definitions, members, and outputs deterministically.
4. Normalize line endings and formatting.
5. In `--write`, update files on disk.
6. In `--check`, fail when existing output differs from expected output.

## Canonical Model Direction

The canonical model should preserve the useful structure from the legacy generator without carrying its app-specific assumptions:

- application definition set
- per-file source metadata
- page feature definitions
- page parameters
- operations
- route, query, and body parameter metadata
- return metadata
- view-model properties
- owned view-model types
- API/request-response models

Scope and routing concepts should remain as neutral model metadata. They should not imply tenant, organization, or other application-specific plumbing in core parsing or generation.

## Config Direction

JSON config should be versioned and explicit. A representative shape:

```json
{
  "version": 1,
  "definitionRoot": "defs",
  "targets": {
    "uiContracts": {
      "kind": "page-ui-engine-interface",
      "directory": "../src/MyApp.Server/UiEngines/Generated",
      "namespace": "MyApp.Server.UiEngines"
    },
    "pageModelBases": {
      "kind": "page-model-base",
      "directory": "../src/MyApp.Web/Pages/Base",
      "namespace": "MyApp.Web.Pages.Base"
    },
    "registrations": {
      "kind": "page-registration-helper",
      "directory": "../src/MyApp.Server/Generated",
      "namespace": "MyApp.Server.Generated"
    }
  }
}
```

Targets own their destination directory, namespace, and artifact kind. Relative paths are resolved from the config file location.

## First-Pass Artifacts

V1 should support:

- UI engine interfaces
- PageModel adapter bases
- registration helpers

V1 should not generate:

- concrete UI engine implementations
- test classes
- app-specific plumbing scaffolding

## Determinism Rules

- Stable definition discovery and sorting.
- Stable member ordering.
- Stable file naming and path layout.
- No timestamps or machine-local content.
- Normalized UTF-8 output and line endings.
- File writes only when content actually changes.

## Legacy Mapping

Useful concepts from the legacy generator to preserve:

- XML-driven page definition model
- application-level view of all definitions
- relative path preservation from definition roots
- thin UI engine contract generation
- thin UI adapter base generation

Legacy behavior to remove:

- PayeWaive namespaces and type assumptions
- generated placeholder tests
- fallback repo paths
- implicit cross-project orchestration
- app-specific route and scope conventions embedded in generation
