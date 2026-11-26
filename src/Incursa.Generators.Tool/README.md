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
```

Local pack and install:

```powershell
dotnet pack .\src\Incursa.Generators.Tool\Incursa.Generators.Tool.csproj -c Debug -o .\nupkgs-test
dotnet tool install Incursa.Generators.Tool --tool-path .\.tools --add-source .\nupkgs-test
```

Ownership and cleanup:

- every generated file carries deterministic ownership metadata
- unfiltered `--check` fails on orphaned generated files
- unfiltered `--write` removes orphaned generated files owned by the tool
- handwritten files are never deleted

See the repository docs and examples for the full config shape and sample XML definitions.
