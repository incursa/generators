<#
.SYNOPSIS
    Run the required blocking validation lane.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Solution = 'Incursa.Generators.slnx',

    [Parameter()]
    [string]$Runsettings = 'runsettings/blocking.runsettings',

    [Parameter()]
    [string]$ResultsDirectory = 'TestResults/blocking',

    [Parameter()]
    [string]$AppDefinitionsConfig = 'examples/AppDefinitions/app-definitions.json',

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
$toolProject = Resolve-RepoPath 'src/Incursa.Generators.Tool/Incursa.Generators.Tool.csproj'
$appDefinitionsConfigPath = Resolve-RepoPath $AppDefinitionsConfig

Write-Host '=== Blocking Lane ===' -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Solution: $(Get-RelativeRepoPath $Solution)" -ForegroundColor Yellow
Write-Host "Runsettings: $(Get-RelativeRepoPath $Runsettings)" -ForegroundColor Yellow
Write-Host "App definitions check: $(Get-RelativeRepoPath $appDefinitionsConfigPath)" -ForegroundColor Yellow
Write-Host "Results directory: $(Get-RelativeRepoPath $resolvedResultsDirectory)" -ForegroundColor Yellow
Write-Host ''

Invoke-RepositoryBuild -Solution $Solution -Configuration $Configuration -NoRestore:$NoRestore -NoBuild:$NoBuild

Write-Host 'Checking generated app-definition outputs for drift...' -ForegroundColor Cyan
$checkArgs = @(
    'run',
    '--project', $toolProject,
    '--configuration', $Configuration,
    '--no-build',
    '--',
    'generate',
    '--config', $appDefinitionsConfigPath,
    '--check'
)

& dotnet @checkArgs
if ($LASTEXITCODE -ne 0) {
    throw "App-definition drift check failed with exit code $LASTEXITCODE."
}

Write-Host ''

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

Write-LaneSummary -Title 'Blocking Lane' -ResultsDirectory $resolvedResultsDirectory -Notes @(
    'Blocking excludes `ExtendedTest`, `Explicit`, and `KnownIssue` tests.',
    "App-definition drift check passed for ``$(Get-RelativeRepoPath $appDefinitionsConfigPath)``."
)

if ($failures.Count -gt 0) {
    throw "Blocking lane failed for: $($failures -join ', ')."
}

Write-Host 'Blocking lane completed successfully.' -ForegroundColor Green
