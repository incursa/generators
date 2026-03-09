<#
.SYNOPSIS
    Run the curated smoke suite for the repository.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Solution = 'Incursa.Generators.slnx',

    [Parameter()]
    [string]$Runsettings = 'runsettings/smoke.runsettings',

    [Parameter()]
    [string]$ResultsDirectory = 'TestResults/smoke',

    [Parameter()]
    [switch]$NoRestore,

    [Parameter()]
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestLane.Common.ps1')

$projects = @(
    'tests/Incursa.Generators.Tests/Incursa.Generators.Tests.csproj',
    'tests/Incursa.Generators.AppDefinitions.Tests/Incursa.Generators.AppDefinitions.Tests.csproj'
)

$resolvedResultsDirectory = New-CleanDirectory $ResultsDirectory

Write-Host '=== Smoke Lane ===' -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Solution: $(Get-RelativeRepoPath $Solution)" -ForegroundColor Yellow
Write-Host "Runsettings: $(Get-RelativeRepoPath $Runsettings)" -ForegroundColor Yellow
Write-Host "Results directory: $(Get-RelativeRepoPath $resolvedResultsDirectory)" -ForegroundColor Yellow
Write-Host ''

Invoke-RepositoryBuild -Solution $Solution -Configuration $Configuration -NoRestore:$NoRestore -NoBuild:$NoBuild

$failures = New-Object System.Collections.Generic.List[string]

foreach ($project in $projects) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
    $exitCode = Invoke-TestRun `
        -Target $project `
        -Configuration $Configuration `
        -ResultsDirectory $resolvedResultsDirectory `
        -Runsettings $Runsettings `
        -LogFileName "$projectName.trx" `
        -NoRestore `
        -NoBuild

    if ($exitCode -ne 0) {
        $failures.Add($projectName)
    }

    Write-Host ''
}

Write-LaneSummary -Title 'Smoke Lane' -ResultsDirectory $resolvedResultsDirectory -Notes @(
    'Curated smoke coverage is driven by tests tagged with `Category=Smoke`.'
)

if ($failures.Count -gt 0) {
    throw "Smoke lane failed for: $($failures -join ', ')."
}

Write-Host 'Smoke lane completed successfully.' -ForegroundColor Green
