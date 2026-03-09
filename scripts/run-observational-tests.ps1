<#
.SYNOPSIS
    Run the non-blocking observational known-issue lane.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Solution = 'Incursa.Generators.slnx',

    [Parameter()]
    [string]$Runsettings = 'runsettings/observational.runsettings',

    [Parameter()]
    [string]$ResultsDirectory = 'TestResults/observational',

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

Write-Host '=== Observational Lane ===' -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Solution: $(Get-RelativeRepoPath $Solution)" -ForegroundColor Yellow
Write-Host "Runsettings: $(Get-RelativeRepoPath $Runsettings)" -ForegroundColor Yellow
Write-Host "Results directory: $(Get-RelativeRepoPath $resolvedResultsDirectory)" -ForegroundColor Yellow
Write-Host ''

Invoke-RepositoryBuild -Solution $Solution -Configuration $Configuration -NoRestore:$NoRestore -NoBuild:$NoBuild

$laneExitCodes = New-Object System.Collections.Generic.List[string]

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

    $laneExitCodes.Add("$projectName=$exitCode")
    Write-Host ''
}

$reports = @(Get-TrxReports -ResultsDirectory $resolvedResultsDirectory)
$notes = New-Object System.Collections.Generic.List[string]
$notes.Add('Observational includes only tests tagged with `Category=KnownIssue` and remains non-blocking by policy.')

if ($reports.Count -gt 0 -and (($reports | Measure-Object -Property Total -Sum).Sum -eq 0)) {
    $notes.Add('No current known-issue tests matched the observational filter.')
}

Write-LaneSummary -Title 'Observational Lane' -ResultsDirectory $resolvedResultsDirectory -Notes $notes

Write-Host "Observational lane exit codes: $($laneExitCodes -join ', ')" -ForegroundColor Yellow
Write-Host 'Observational lane completed. Test failures remain visible in artifacts but do not fail the lane.' -ForegroundColor Yellow
exit 0
