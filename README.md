# Incursa.Generators

This repository contains two generation approaches:

- [`src/Incursa.Generators`](src/Incursa.Generators): Roslyn source generators for project-local JSON/XML driven type generation.
- [`src/Incursa.Generators.Tool`](src/Incursa.Generators.Tool): a deterministic .NET tool for explicit XML definition driven generation into config-selected output directories.

The deterministic tool exists for scenarios where generated outputs should be explicit, versionable, and able to target multiple projects without hidden build-time orchestration.

Start here for the new tool:

- [`docs/app-definitions-tool.md`](docs/app-definitions-tool.md)
- [`docs/app-definitions-tool-design.md`](docs/app-definitions-tool-design.md)
- [`docs/testing-operating-model.md`](docs/testing-operating-model.md)
- [`examples/AppDefinitions/app-definitions.json`](examples/AppDefinitions/app-definitions.json)

See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE) for licensing and attribution information.
