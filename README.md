# Incursa.Generators

This repository now contains two generation approaches:

- [`src/Incursa.Generators`](/C:/src/incursa/generators/src/Incursa.Generators): Roslyn source generators for project-local JSON/XML driven type generation.
- [`src/Incursa.Generators.Tool`](/C:/src/incursa/generators/src/Incursa.Generators.Tool): a deterministic .NET tool for explicit XML definition driven generation into config-selected output directories.
- [`src/Incursa.Generators.SpecTrace`](/C:/src/incursa/generators/src/Incursa.Generators.SpecTrace): SpecTrace export helpers, including the ClickUp requirement CSV exporter.

The deterministic tool exists for scenarios where generated outputs should be explicit, versionable, and able to target multiple projects without hidden build-time orchestration.

Start here for the new tool:

- [`docs/app-definitions-tool.md`](/C:/src/incursa/generators/docs/app-definitions-tool.md)
- [`docs/app-definitions-tool-design.md`](/C:/src/incursa/generators/docs/app-definitions-tool-design.md)
- [`docs/testing-operating-model.md`](/C:/src/incursa/generators/docs/testing-operating-model.md)
- [`examples/AppDefinitions/app-definitions.json`](/C:/src/incursa/generators/examples/AppDefinitions/app-definitions.json)

SpecTrace-to-ClickUp CSV exports now live in the dedicated ClickUp tooling folder and can be regenerated with:

```powershell
..\ClickUpTooling\scripts\Export-IncursaSpecTraceClickUpCsv.ps1 -Clean
```

The default export script writes repository rollup CSVs and splits
`SPEC-*-RFC*.json` specification files into independent `RFC####.csv` files.
The previous all-in-one rollup behavior is retained at
`..\ClickUpTooling\scripts\Export-IncursaSpecTraceClickUpCsv.Rollup.ps1`.

See [`LICENSE`](/C:/src/incursa/generators/LICENSE) and [`NOTICE`](/C:/src/incursa/generators/NOTICE) for licensing and attribution information.
