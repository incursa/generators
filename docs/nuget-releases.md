# NuGet releases

The two packages in this repository use ordinary semantic versions. Calendar- or date-based package versions are intentionally rejected by CI.

Package versions are declared in both the project file and `eng/package-versions.json`. The validation script requires them to match and rejects a major version in the range 2000 through 2099.

The packages release independently:

- `generator`: `Incursa.Generators`
- `tool`: `Incursa.Generators.Tool`

A normal push builds, packs, and validates both packages, but does not publish either one. Publishing requires one of these explicit actions:

1. Run the `Main/Release CI/CD` workflow manually, select a release group, and enable `publish_packages`.
2. Push a release-group tag named `generator-v<version>` or `tool-v<version>`.

Before requesting a release, update the selected package's project version and its matching entry in `eng/package-versions.json`. The workflow publishes only the selected release group and uses `--skip-duplicate` as a final safety measure.

Local validation:

```powershell
./eng/Test-PackageVersions.ps1
dotnet restore Incursa.Generators.slnx
dotnet build Incursa.Generators.slnx --configuration Release --no-restore
./eng/Pack-Packages.ps1 -ReleaseGroup all -NoBuild -NoRestore
./eng/Test-PackageArtifacts.ps1 -ReleaseGroup all
```
