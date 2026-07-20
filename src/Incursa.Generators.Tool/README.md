# Incursa.Generators.Tool

`Incursa.Generators.Tool` is a deterministic .NET tool for XML definition driven code generation.

The first supported scenario is application/page-feature generation around the UI engine pattern:

- page contract models
- UI engine interfaces
- Razor `PageModel` adapter bases
- registration helpers

Commands:

```text
incursa-appdefs validate --config <path>
incursa-appdefs generate --config <path> --write
incursa-appdefs generate --config <path> --check
incursa-appdefs export-clickup-requirements --repo-root <path> --repository <owner/name> --out <path> [--filter <glob-or-path>] [--exclude-filter <glob-or-path>] [--skip-lifecycle-state-derivation]
```

## SpecTrace ClickUp requirement CSV export

`export-clickup-requirements` reads canonical SpecTrace JSON specification artifacts from
`specs/requirements/**/*.json` and writes a ClickUp spreadsheet import CSV for `REQ-*`
records. It does not call ClickUp and does not mutate source repositories.
The v1 CSV hardcodes `Assigned To` as `samuel@incursa.com`. Non-RFC requirements
emit task type `Impl Req`; RFC specification requirements emit task type
`Spec Req`. Implementation state
uses explicit supported state fields when present. Otherwise it resolves linked
work-item artifact statuses: complete/implemented work maps to `Implemented`,
active mixed work maps to `Partial`, blocked/deferred/superseded/retired statuses
map directly, unresolved work-item links map to `Planned`, and missing work-item
links map to `Not started`. If no stronger artifact state is present, a
requirement ID found in repository source code is treated as implementation
evidence and maps the row to `Implemented`. Verification state follows linked verification
artifact statuses: passed maps to `Passed`, failed/blocked/waived/obsolete map
directly, unresolved verification links map to `Planned`, and missing
verification links map to `None`. If no failed/blocked/waived/obsolete
verification artifact is present, a requirement ID found in repository tests is
treated as verification evidence and maps the row to `Passed`. Trace status is
`Unlinked` without downstream trace links or direct evidence, `Traceable` when
downstream trace or direct evidence exists, and `Auditable` when implementation
is `Implemented` and verification is `Passed`.
`Description` is the requirement statement only. Trace details are emitted in
the existing `Architecture IDs`, `Work Item IDs`, and `Verification IDs` columns
with one value per line when multiple values exist.
`Source Section` is reserved for explicit section-like source locations, such
as RFC sections, clauses, or document headings. Implementation paths, test
paths, code symbols, and trace-reference material are emitted in `Source Refs`
with one value per line. The tool warns when a row has blank `Source Section`
but populated `Source Refs`, which is expected for implementation-oriented
requirements.

Example:

```powershell
dotnet run --project C:\shared\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -- export-clickup-requirements `
  --repo-root C:\shared\src\incursa\quic-dotnet `
  --repository incursa/quic-dotnet `
  --source-commit <sha> `
  --out C:\shared\src\incursa\ClickUpTooling\artifacts\clickup-requirements-import\quic-dotnet.csv
```

For all local Incursa repositories that use SpecTrace:

```powershell
C:\shared\src\incursa\ClickUpTooling\scripts\Export-IncursaSpecTraceClickUpCsv.ps1 -Clean
```

The batch script writes `<<reponame>>.csv` files and matching
`<<reponame>>.requirements-import-manifest.json` files to
`C:\shared\src\incursa\ClickUpTooling\artifacts\clickup-requirements-import`.
RFC specification files named like `SPEC-*-RFC*.json` are excluded from the
repository rollup and exported as independent `RFC####.csv` files. RFC exports
use the same columns, but leave `Implementation State`, `Verification State`,
and `Trace Status` blank by passing `--skip-lifecycle-state-derivation`.
For RFC exports, RFC section locators such as `RFC 9000 §2.2` or
`RFC9000-S2P2-B1-P1-S2` are emitted in `Source Section`; `Source Refs` is
limited to external RFC document URLs and suppresses code, test, architecture,
work-item, and verification references.
The prior all-in-one rollup behavior is preserved in
`C:\shared\src\incursa\ClickUpTooling\scripts\Export-IncursaSpecTraceClickUpCsv.Rollup.ps1`.

Local pack and install:

```powershell
dotnet pack C:\src\incursa\generators\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -c Debug -o C:\src\incursa\generators\nupkgs-test
dotnet tool install Incursa.Generators.Tool --tool-path C:\src\incursa\generators\.tools --add-source C:\src\incursa\generators\nupkgs-test
```

Ownership and cleanup:

- every generated file carries deterministic ownership metadata
- unfiltered `--check` fails on orphaned generated files
- unfiltered `--write` removes orphaned generated files owned by the tool
- handwritten files are never deleted

See the repository docs and examples for the full config shape and sample XML definitions.
