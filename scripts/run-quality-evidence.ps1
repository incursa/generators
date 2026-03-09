<#
.SYNOPSIS
    Produce a lightweight evidence bundle from advisory results and coverage.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ResultsDirectory = 'TestResults/advisory',

    [Parameter()]
    [string]$OutputDirectory = 'artifacts/quality/testing'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestLane.Common.ps1')

$resolvedResultsDirectory = Resolve-RepoPath $ResultsDirectory
$resolvedOutputDirectory = New-CleanDirectory $OutputDirectory

$trxReports = @(Get-TrxReports -ResultsDirectory $resolvedResultsDirectory)
$trxTotals = Get-TrxTotals -Reports $trxReports
$coverageReports = @(Get-CoberturaReports -ResultsDirectory $resolvedResultsDirectory)

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add('# Quality Evidence')
$summaryLines.Add('')
$summaryLines.Add("- Generated: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK')")
$summaryLines.Add("- Results directory: ``$(Get-RelativeRepoPath $resolvedResultsDirectory)``")
$summaryLines.Add("- Test totals: total=$($trxTotals.Total), passed=$($trxTotals.Passed), failed=$($trxTotals.Failed), skipped=$($trxTotals.Skipped)")
$summaryLines.Add("- Test result files: $($trxReports.Count)")
$summaryLines.Add("- Coverage files: $($coverageReports.Count)")
$summaryLines.Add('')
$summaryLines.Add('## Test Results')
$summaryLines.Add('')
$summaryLines.Add('| Suite | Outcome | Counts | File |')
$summaryLines.Add('|---|---|---|---|')

if ($trxReports.Count -eq 0) {
    $summaryLines.Add('| n/a | No `.trx` files found | n/a | n/a |')
}
else {
    foreach ($report in $trxReports) {
        $suiteName = [System.IO.Path]::GetFileNameWithoutExtension($report.File)
        $summaryLines.Add("| $suiteName | $($report.Outcome) | total=$($report.Total), passed=$($report.Passed), failed=$($report.Failed), skipped=$($report.Skipped) | ``$($report.RelativePath)`` |")
    }
}

$summaryLines.Add('')
$summaryLines.Add('## Coverage')
$summaryLines.Add('')
$summaryLines.Add('| File | Line Coverage | Branch Coverage |')
$summaryLines.Add('|---|---|---|')

if ($coverageReports.Count -eq 0) {
    $summaryLines.Add('| n/a | n/a | n/a |')
}
else {
    foreach ($coverageReport in $coverageReports) {
        $lineCoverage = if ($null -ne $coverageReport.LineRate) { '{0:P2}' -f $coverageReport.LineRate } else { 'n/a' }
        $branchCoverage = if ($null -ne $coverageReport.BranchRate) { '{0:P2}' -f $coverageReport.BranchRate } else { 'n/a' }
        $summaryLines.Add("| ``$($coverageReport.RelativePath)`` | $lineCoverage | $branchCoverage |")
    }
}

$evidence = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    resultsDirectory = Get-RelativeRepoPath $resolvedResultsDirectory
    tests = [ordered]@{
        totals = [ordered]@{
            total = $trxTotals.Total
            passed = $trxTotals.Passed
            failed = $trxTotals.Failed
            skipped = $trxTotals.Skipped
        }
        reports = @(
            foreach ($report in $trxReports) {
                [ordered]@{
                    path = $report.RelativePath
                    outcome = $report.Outcome
                    total = $report.Total
                    passed = $report.Passed
                    failed = $report.Failed
                    skipped = $report.Skipped
                }
            }
        )
    }
    coverage = @(
        foreach ($coverageReport in $coverageReports) {
            [ordered]@{
                path = $coverageReport.RelativePath
                lineRate = $coverageReport.LineRate
                branchRate = $coverageReport.BranchRate
            }
        }
    )
}

$summaryPath = Join-Path $resolvedOutputDirectory 'summary.md'
$evidencePath = Join-Path $resolvedOutputDirectory 'evidence.json'

Set-Content -Path $summaryPath -Value ($summaryLines -join [Environment]::NewLine)
$evidence | ConvertTo-Json -Depth 8 | Set-Content -Path $evidencePath

Write-Host "Quality evidence written to $(Get-RelativeRepoPath $resolvedOutputDirectory)." -ForegroundColor Green
