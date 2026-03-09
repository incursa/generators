Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-RepoRoot {
    return $script:RepoRoot
}

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $Path))
}

function New-CleanDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedPath = Resolve-RepoPath $Path
    if (Test-Path $resolvedPath) {
        Remove-Item -Path $resolvedPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $resolvedPath -Force | Out-Null
    return $resolvedPath
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedPath = Resolve-RepoPath $Path
    New-Item -ItemType Directory -Path $resolvedPath -Force | Out-Null
    return $resolvedPath
}

function Get-RelativeRepoPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return [System.IO.Path]::GetRelativePath($script:RepoRoot, (Resolve-RepoPath $Path))
}

function Invoke-RepositoryBuild {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$Solution = 'Incursa.Generators.slnx',

        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Release',

        [Parameter()]
        [switch]$NoRestore,

        [Parameter()]
        [switch]$NoBuild
    )

    $solutionPath = Resolve-RepoPath $Solution

    Push-Location $script:RepoRoot
    try {
        if (-not $NoRestore) {
            Write-Host "Restoring solution: $(Get-RelativeRepoPath $solutionPath)" -ForegroundColor Cyan
            & dotnet restore $solutionPath
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed with exit code $LASTEXITCODE."
            }

            Write-Host ''
        }

        if (-not $NoBuild) {
            $buildArgs = @(
                'build',
                $solutionPath,
                '--configuration', $Configuration,
                '-p:GeneratePackageOnBuild=false'
            )

            if (-not $NoRestore) {
                $buildArgs += '--no-restore'
            }

            Write-Host "Building solution: $(Get-RelativeRepoPath $solutionPath)" -ForegroundColor Cyan
            & dotnet @buildArgs
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build failed with exit code $LASTEXITCODE."
            }

            Write-Host ''
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-TestRun {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Target,

        [Parameter(Mandatory)]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration,

        [Parameter(Mandatory)]
        [string]$ResultsDirectory,

        [Parameter(Mandatory)]
        [string]$LogFileName,

        [Parameter()]
        [string]$Runsettings,

        [Parameter()]
        [string[]]$AdditionalArguments = @(),

        [Parameter()]
        [switch]$NoRestore,

        [Parameter()]
        [switch]$NoBuild
    )

    $targetPath = Resolve-RepoPath $Target
    $resolvedResultsDirectory = Ensure-Directory $ResultsDirectory

    $testArgs = @(
        'test',
        $targetPath,
        '--configuration', $Configuration,
        '--results-directory', $resolvedResultsDirectory,
        '--logger', "trx;LogFileName=$LogFileName"
    )

    if (-not [string]::IsNullOrWhiteSpace($Runsettings)) {
        $testArgs += @('--settings', (Resolve-RepoPath $Runsettings))
    }

    if ($NoRestore) {
        $testArgs += '--no-restore'
    }

    if ($NoBuild) {
        $testArgs += '--no-build'
    }

    if ($AdditionalArguments.Count -gt 0) {
        $testArgs += $AdditionalArguments
    }

    Write-Host "Executing: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
    & dotnet @testArgs | Out-Host
    return $LASTEXITCODE
}

function Get-TrxReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ResultsDirectory
    )

    $resolvedResultsDirectory = Resolve-RepoPath $ResultsDirectory
    if (-not (Test-Path $resolvedResultsDirectory)) {
        return @()
    }

    $trxFiles = Get-ChildItem -Path $resolvedResultsDirectory -Filter *.trx -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName

    $reports = New-Object System.Collections.Generic.List[object]

    foreach ($trxFile in $trxFiles) {
        $report = [ordered]@{
            File = $trxFile.FullName
            RelativePath = Get-RelativeRepoPath $trxFile.FullName
            Outcome = 'Unknown'
            Total = 0
            Passed = 0
            Failed = 0
            Skipped = 0
        }

        try {
            [xml]$trx = Get-Content -Path $trxFile.FullName -Raw
            $counters = $trx.TestRun.ResultSummary.Counters
            $report.Outcome = [string]$trx.TestRun.ResultSummary.outcome
            $report.Total = [int]$counters.total
            $report.Passed = [int]$counters.passed
            $report.Failed = [int]$counters.failed
            $report.Skipped = [int]$counters.notExecuted
        }
        catch {
            $report.Outcome = 'Unreadable'
        }

        $reports.Add([pscustomobject]$report)
    }

    return $reports
}

function Get-TrxTotals {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$Reports
    )

    if ($Reports.Count -eq 0) {
        return [pscustomobject]@{
            Total = 0
            Passed = 0
            Failed = 0
            Skipped = 0
        }
    }

    return [pscustomobject]@{
        Total = [int](($Reports | Measure-Object -Property Total -Sum).Sum)
        Passed = [int](($Reports | Measure-Object -Property Passed -Sum).Sum)
        Failed = [int](($Reports | Measure-Object -Property Failed -Sum).Sum)
        Skipped = [int](($Reports | Measure-Object -Property Skipped -Sum).Sum)
    }
}

function Write-LaneSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title,

        [Parameter(Mandatory)]
        [string]$ResultsDirectory,

        [Parameter()]
        [string[]]$Notes = @()
    )

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        return
    }

    $reports = @(Get-TrxReports -ResultsDirectory $ResultsDirectory)
    $totals = Get-TrxTotals -Reports $reports
    $summaryLines = New-Object System.Collections.Generic.List[string]
    $summaryLines.Add("## $Title")
    $summaryLines.Add('')
    $summaryLines.Add("- Results directory: ``$(Get-RelativeRepoPath $ResultsDirectory)``")
    $summaryLines.Add("- Totals: total=$($totals.Total), passed=$($totals.Passed), failed=$($totals.Failed), skipped=$($totals.Skipped)")

    foreach ($note in $Notes) {
        $summaryLines.Add("- $note")
    }

    $summaryLines.Add('')
    $summaryLines.Add('| Suite | Outcome | Counts | Results |')
    $summaryLines.Add('|---|---|---|---|')

    if ($reports.Count -eq 0) {
        $summaryLines.Add('| n/a | No `.trx` files found | n/a | n/a |')
    }
    else {
        foreach ($report in $reports) {
            $suiteName = [System.IO.Path]::GetFileNameWithoutExtension($report.File)
            $summaryLines.Add("| $suiteName | $($report.Outcome) | total=$($report.Total), passed=$($report.Passed), failed=$($report.Failed), skipped=$($report.Skipped) | ``$($report.RelativePath)`` |")
        }
    }

    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value ($summaryLines -join [Environment]::NewLine)
}

function Get-CoberturaReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ResultsDirectory
    )

    $resolvedResultsDirectory = Resolve-RepoPath $ResultsDirectory
    if (-not (Test-Path $resolvedResultsDirectory)) {
        return @()
    }

    $coverageFiles = Get-ChildItem -Path $resolvedResultsDirectory -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName

    $reports = New-Object System.Collections.Generic.List[object]

    foreach ($coverageFile in $coverageFiles) {
        $report = [ordered]@{
            File = $coverageFile.FullName
            RelativePath = Get-RelativeRepoPath $coverageFile.FullName
            LineRate = $null
            BranchRate = $null
        }

        try {
            [xml]$coverage = Get-Content -Path $coverageFile.FullName -Raw
            if ($coverage.DocumentElement.LocalName -ne 'coverage') {
                continue
            }

            $report.LineRate = [double]$coverage.coverage.'line-rate'
            $report.BranchRate = [double]$coverage.coverage.'branch-rate'
        }
        catch {
            continue
        }

        $reports.Add([pscustomobject]$report)
    }

    return $reports
}
