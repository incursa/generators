[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$VersionsPath = (Join-Path $PSScriptRoot "package-versions.json")
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $VersionsPath)) {
    throw "Package version manifest not found at '$VersionsPath'."
}

$manifest = Get-Content -LiteralPath $VersionsPath -Raw | ConvertFrom-Json
$seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$seenGroups = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($package in @($manifest.packages)) {
    $packageId = [string]$package.packageId
    $version = [string]$package.version
    $releaseGroup = [string]$package.releaseGroup
    $projectPath = Join-Path $RepoRoot ([string]$package.projectPath)

    if (-not $seenIds.Add($packageId)) {
        throw "Duplicate package id '$packageId' in the version manifest."
    }

    if ($releaseGroup -notin @("generator", "tool")) {
        throw "Package '$packageId' has unsupported release group '$releaseGroup'."
    }

    [void]$seenGroups.Add($releaseGroup)

    if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') {
        throw "Package '$packageId' has non-semantic version '$version'."
    }

    $major = [int]($version.Split('.')[0])
    if ($major -ge 2000 -and $major -le 2099) {
        throw "Package '$packageId' uses a forbidden calendar version '$version'."
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Package '$packageId' project was not found at '$projectPath'."
    }

    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $packageIdNode = $project.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='PackageId']")
    $versionNode = $project.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='Version']")
    $projectPackageId = if ($null -eq $packageIdNode) { "" } else { ([string]$packageIdNode.InnerText).Trim() }
    $projectVersion = if ($null -eq $versionNode) { "" } else { ([string]$versionNode.InnerText).Trim() }

    if ($projectPackageId -ne $packageId) {
        throw "Project '$projectPath' declares package id '$projectPackageId'; expected '$packageId'."
    }

    if ($projectVersion -ne $version) {
        throw "Project '$projectPath' declares version '$projectVersion'; expected '$version'."
    }
}

foreach ($requiredGroup in @("generator", "tool")) {
    if (-not $seenGroups.Contains($requiredGroup)) {
        throw "Required release group '$requiredGroup' is missing from the version manifest."
    }
}

Write-Host "Validated $($seenIds.Count) semantic package version(s); calendar versions are forbidden."
