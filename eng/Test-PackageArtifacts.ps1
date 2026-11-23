[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path $PSScriptRoot "../nupkgs"),
    [string]$VersionsPath = (Join-Path $PSScriptRoot "package-versions.json"),
    [ValidateSet("all", "generator", "tool")]
    [string]$ReleaseGroup = "all"
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ElementText {
    param([System.Xml.XmlNode]$Parent, [string]$Name)
    $node = $Parent.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return "" }
    return ([string]$node.InnerText).Trim()
}

function Add-Failure {
    param([System.Collections.Generic.List[string]]$Failures, [string]$Message)
    $Failures.Add($Message)
}

if (-not (Test-Path -LiteralPath $VersionsPath)) {
    throw "Package version manifest not found at '$VersionsPath'."
}

$manifest = Get-Content -LiteralPath $VersionsPath -Raw | ConvertFrom-Json
$expectedPackages = @($manifest.packages)
if ($ReleaseGroup -ne "all") {
    $expectedPackages = @($expectedPackages | Where-Object { $_.releaseGroup -eq $ReleaseGroup })
}

$expectedById = @{}
foreach ($package in $expectedPackages) {
    $expectedById[[string]$package.packageId] = $package
}

$packageFiles = @(Get-ChildItem -LiteralPath $PackagePath -Filter "*.nupkg" -File -ErrorAction SilentlyContinue | Sort-Object Name)
if ($packageFiles.Count -eq 0) {
    throw "No NuGet packages found at '$PackagePath'."
}

$failures = [System.Collections.Generic.List[string]]::new()
$artifactsById = @{}

foreach ($packageFile in $packageFiles) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packageFile.FullName)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($nuspecEntries.Count -ne 1) {
            Add-Failure $failures "$($packageFile.Name): expected exactly one .nuspec file; found $($nuspecEntries.Count)."
            continue
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            Add-Failure $failures "$($packageFile.Name): package metadata was not found."
            continue
        }

        $packageId = Get-ElementText $metadata "id"
        $packageVersion = Get-ElementText $metadata "version"
        if ([string]::IsNullOrWhiteSpace($packageId)) {
            Add-Failure $failures "$($packageFile.Name): package id is missing."
            continue
        }

        if (-not $expectedById.ContainsKey($packageId)) {
            Add-Failure $failures "$($packageFile.Name): '$packageId' is not expected in release group '$ReleaseGroup'."
        }
        if ($artifactsById.ContainsKey($packageId)) {
            Add-Failure $failures "$($packageFile.Name): duplicate artifact for '$packageId'."
        }
        else {
            $artifactsById[$packageId] = $packageVersion
        }

        foreach ($requiredElement in @("title", "authors", "description", "projectUrl", "tags", "icon", "readme")) {
            if ([string]::IsNullOrWhiteSpace((Get-ElementText $metadata $requiredElement))) {
                Add-Failure $failures "$($packageFile.Name): required metadata '$requiredElement' is missing."
            }
        }

        $license = $metadata.SelectSingleNode("*[local-name()='license']")
        if ($null -eq $license -or $license.GetAttribute("type") -ne "expression" -or [string]::IsNullOrWhiteSpace($license.InnerText)) {
            Add-Failure $failures "$($packageFile.Name): an SPDX license expression is required."
        }
        $repository = $metadata.SelectSingleNode("*[local-name()='repository']")
        if ($null -eq $repository -or [string]::IsNullOrWhiteSpace($repository.GetAttribute("url")) -or $repository.GetAttribute("type") -ne "git") {
            Add-Failure $failures "$($packageFile.Name): git repository metadata is missing or incomplete."
        }

        foreach ($assetName in @((Get-ElementText $metadata "icon"), (Get-ElementText $metadata "readme"))) {
            if ([string]::IsNullOrWhiteSpace($assetName)) { continue }
            $normalizedAssetName = $assetName.Replace("\", "/").TrimStart("/")
            $assetEntry = $archive.Entries | Where-Object { $_.FullName.Equals($normalizedAssetName, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
            if ($null -eq $assetEntry) {
                Add-Failure $failures "$($packageFile.Name): declared asset '$assetName' is not embedded in the package."
            }
            elseif ($assetEntry.Length -eq 0) {
                Add-Failure $failures "$($packageFile.Name): embedded asset '$assetName' is empty."
            }
            elseif ($normalizedAssetName.EndsWith(".png", [System.StringComparison]::OrdinalIgnoreCase) -and $assetEntry.Length -gt 1MB) {
                Add-Failure $failures "$($packageFile.Name): package icon '$assetName' exceeds NuGet's 1 MB limit."
            }
        }
    }
    finally { $archive.Dispose() }
}

foreach ($package in $expectedPackages) {
    $packageId = [string]$package.packageId
    if (-not $artifactsById.ContainsKey($packageId)) {
        Add-Failure $failures "Expected artifact for '$packageId' was not found."
        continue
    }
    if ($artifactsById[$packageId] -ne [string]$package.version) {
        Add-Failure $failures "Artifact '$packageId' has version '$($artifactsById[$packageId])'; expected '$($package.version)'."
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "NuGet package artifact validation failed with $($failures.Count) error(s)."
}

Write-Host "Validated $($packageFiles.Count) NuGet package artifact(s) for release group '$ReleaseGroup', including required metadata, icon, and README assets."
