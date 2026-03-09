<#
.SYNOPSIS
    Run the broader advisory quality lane and collect coverage artifacts.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Solution = 'Incursa.Generators.slnx',

    [Parameter()]
    [string]$BlockingRunsettings = 'runsettings/blocking.runsettings',

    [Parameter()]
    [string]$ResultsDirectory = 'TestResults/advisory',

    [Parameter()]
    [switch]$NoRestore,

    [Parameter()]
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestLane.Common.ps1')

$resolvedResultsDirectory = New-CleanDirectory $ResultsDirectory
$coverageDirectory = Ensure-Directory (Join-Path $resolvedResultsDirectory 'coverage')

$suiteEntries = @(
    @{
        Name = 'Incursa.Generators.AppDefinitions.Tests'
        Target = 'tests/Incursa.Generators.AppDefinitions.Tests/Incursa.Generators.AppDefinitions.Tests.csproj'
        Runsettings = $BlockingRunsettings
        Filter = $null
    },
    @{
        Name = 'Incursa.Generators.Tests'
        Target = 'tests/Incursa.Generators.Tests/Incursa.Generators.Tests.csproj'
        Runsettings = $BlockingRunsettings
        Filter = $null
    },
    @{
        Name = 'Incursa.Generators.Tests-Extended'
        Target = 'tests/Incursa.Generators.Tests/Incursa.Generators.Tests.csproj'
        Runsettings = $null
        Filter = 'Category=ExtendedTest'
    }
)

Write-Host '=== Advisory Quality Lane ===' -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Solution: $(Get-RelativeRepoPath $Solution)" -ForegroundColor Yellow
Write-Host "Blocking runsettings: $(Get-RelativeRepoPath $BlockingRunsettings)" -ForegroundColor Yellow
Write-Host "Results directory: $(Get-RelativeRepoPath $resolvedResultsDirectory)" -ForegroundColor Yellow
Write-Host ''

Invoke-RepositoryBuild -Solution $Solution -Configuration $Configuration -NoRestore:$NoRestore -NoBuild:$NoBuild

$failures = New-Object System.Collections.Generic.List[string]

foreach ($suiteEntry in $suiteEntries) {
    $coverageFile = Join-Path $coverageDirectory "$($suiteEntry.Name).cobertura.xml"
    $additionalArguments = @(
        '/p:CollectCoverage=true',
        '/p:CoverletOutputFormat=cobertura',
        "/p:CoverletOutput=$coverageFile",
        '/p:IncludeTestAssembly=false'
    )

    if (-not [string]::IsNullOrWhiteSpace($suiteEntry.Filter)) {
        $additionalArguments += @('--filter', $suiteEntry.Filter)
    }

    $exitCode = Invoke-TestRun `
        -Target $suiteEntry.Target `
        -Configuration $Configuration `
        -ResultsDirectory $resolvedResultsDirectory `
        -Runsettings $suiteEntry.Runsettings `
        -LogFileName "$($suiteEntry.Name).trx" `
        -AdditionalArguments $additionalArguments `
        -NoRestore `
        -NoBuild

    if ($exitCode -ne 0) {
        $failures.Add($suiteEntry.Name)
    }

    Write-Host ''
}

Write-LaneSummary -Title 'Advisory Quality Lane' -ResultsDirectory $resolvedResultsDirectory -Notes @(
    'Advisory reruns the blocking suites with coverage plus the `ExtendedTest` performance suite.',
    "Coverage artifacts are written under ``$(Get-RelativeRepoPath $coverageDirectory)``."
)

if ($failures.Count -gt 0) {
    throw "Advisory lane failed for: $($failures -join ', ')."
}

Write-Host 'Advisory quality lane completed successfully.' -ForegroundColor Green
