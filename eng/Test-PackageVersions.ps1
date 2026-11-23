[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$VersionsPath = (Join-Path $PSScriptRoot "package-versions.json")
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function Assert-SemanticPackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $semanticVersion = [regex]::Match(
        $Version,
        '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$')
    if (-not $semanticVersion.Success) {
        throw "Package '$PackageId' has non-semantic version '$Version'."
    }

    $prerelease = $semanticVersion.Groups["prerelease"].Value
    foreach ($identifier in @($prerelease.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries))) {
        if ($identifier -match '^[0-9]+$' -and $identifier.Length -gt 1 -and $identifier.StartsWith('0', [System.StringComparison]::Ordinal)) {
            throw "Package '$PackageId' has non-semantic version '$Version': numeric prerelease identifiers cannot contain leading zeroes."
        }
    }

    $major = $semanticVersion.Groups["major"].Value
    if ($major -match '^20[0-9]{2}$') {
        throw "Package '$PackageId' uses a forbidden calendar version '$Version'."
    }
}

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

    Assert-SemanticPackageVersion -PackageId $packageId -Version $version

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
