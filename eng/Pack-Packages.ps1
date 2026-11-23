[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$VersionsPath = (Join-Path $PSScriptRoot "package-versions.json"),
    [string]$Configuration = "Release",
    [string]$OutputPath = (Join-Path $RepoRoot "nupkgs"),
    [ValidateSet("all", "generator", "tool")]
    [string]$ReleaseGroup = "all",
    [switch]$NoBuild,
    [switch]$NoRestore
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "Test-PackageVersions.ps1") -RepoRoot $RepoRoot -VersionsPath $VersionsPath

$manifest = Get-Content -LiteralPath $VersionsPath -Raw | ConvertFrom-Json
$packages = @($manifest.packages)
if ($ReleaseGroup -ne "all") {
    $packages = @($packages | Where-Object { $_.releaseGroup -eq $ReleaseGroup })
}

if ($packages.Count -eq 0) {
    throw "Release group '$ReleaseGroup' did not select any packages."
}

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
Write-Host "Packing release group '$ReleaseGroup' with $($packages.Count) package(s)."

foreach ($package in $packages) {
    $projectPath = Join-Path $RepoRoot ([string]$package.projectPath)
    $arguments = @("pack", $projectPath, "--configuration", $Configuration, "--output", $OutputPath)
    if ($NoBuild) {
        $arguments += "--no-build"
    }
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$($package.packageId)' with exit code $LASTEXITCODE."
    }
}
