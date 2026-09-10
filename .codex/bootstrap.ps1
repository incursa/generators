#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$WorkerUser = 'samuel',
    [switch]$RunTests,
    [switch]$VerifyOnly,
    [switch]$SkipCodexUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory
    )

    $previousLocation = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $previousLocation = Get-Location
            Set-Location -LiteralPath $WorkingDirectory
        }

        & $File @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE`: $File $($Arguments -join ' ')"
        }
    }
    finally {
        if ($null -ne $previousLocation) {
            Set-Location -LiteralPath $previousLocation
        }
    }
}

function Test-IsRoot {
    return [int]((& id -u | Out-String).Trim()) -eq 0
}

function Get-WorkerHome {
    $entry = (& getent passwd $WorkerUser 2>$null | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($entry)) {
        throw "Linux user '$WorkerUser' does not exist."
    }

    return $entry.Split(':')[5]
}

function Invoke-Root {
    param([Parameter(Mandatory)][string]$File, [string[]]$Arguments = @())

    if (Test-IsRoot) {
        Invoke-Native -File $File -Arguments $Arguments
        return
    }

    if (-not (Get-Command sudo -ErrorAction SilentlyContinue)) {
        throw 'Root privileges or sudo are required to provision the worker.'
    }

    Invoke-Native -File 'sudo' -Arguments (@($File) + $Arguments)
}

function Invoke-AsWorker {
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory
    )

    $currentUser = (& id -un | Out-String).Trim()
    if ($currentUser -eq $WorkerUser) {
        Invoke-Native -File $File -Arguments $Arguments -WorkingDirectory $WorkingDirectory
        return
    }

    if (-not (Test-IsRoot)) {
        throw "Cannot run worker commands as '$WorkerUser' from '$currentUser'."
    }

    $workerHome = Get-WorkerHome
    $workerPath = "$workerHome/.local/bin:$workerHome/bin:/usr/local/bin:/usr/bin:/bin"
    $runUserArguments = @('-u', $WorkerUser, '--', 'env', "HOME=$workerHome", "PATH=$workerPath", $File) + $Arguments
    Invoke-Native -File 'runuser' -Arguments $runUserArguments -WorkingDirectory $WorkingDirectory
}

function Test-WorkerCommand {
    param([Parameter(Mandatory)][string]$Command)

    $workerHome = Get-WorkerHome
    $workerPath = "$workerHome/.local/bin:$workerHome/bin:/usr/local/bin:/usr/bin:/bin"
    if ((& id -un | Out-String).Trim() -eq $WorkerUser) {
        & env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command *> $null
    }
    elseif (Test-IsRoot) {
        & runuser -u $WorkerUser -- env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command *> $null
    }
    else {
        return $false
    }

    return $LASTEXITCODE -eq 0
}

function Test-ConfigProperty {
    param([Parameter(Mandatory)]$Object, [Parameter(Mandatory)][string]$Name)
    return $null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name
}

function Ensure-Node {
    param([Parameter(Mandatory)]$NodeConfig)

    $requiredMajor = [int]$NodeConfig.majorVersion
    $actualMajor = 0
    if (Get-Command node -ErrorAction SilentlyContinue) {
        [void][int]::TryParse((& node --version).TrimStart('v').Split('.')[0], [ref]$actualMajor)
    }

    if ($actualMajor -ne $requiredMajor) {
        if ($VerifyOnly) {
            throw "Node.js $requiredMajor.x is required; the active version is $actualMajor.x."
        }

        $architecture = switch ((& uname -m | Out-String).Trim()) {
            'x86_64' { 'x64' }
            'aarch64' { 'arm64' }
            default { throw "Unsupported Node.js worker architecture: $_" }
        }
        $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "node-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
        try {
            $releaseRoot = "https://nodejs.org/dist/latest-v$requiredMajor.x"
            Invoke-Native -File 'curl' -Arguments @('-fsSL', "$releaseRoot/SHASUMS256.txt", '-o', (Join-Path $temporaryDirectory 'SHASUMS256.txt'))
            $archivePattern = "  (node-v[^ ]+-linux-$architecture[.]tar[.]xz)$"
            $checksumLine = Get-Content -LiteralPath (Join-Path $temporaryDirectory 'SHASUMS256.txt') |
                Where-Object { $_ -match $archivePattern } |
                Select-Object -First 1
            if ($null -eq $checksumLine -or $checksumLine -notmatch $archivePattern) {
                throw "Unable to locate a Node.js $requiredMajor.x archive for $architecture."
            }

            $archiveName = $Matches[1]
            $archivePath = Join-Path $temporaryDirectory $archiveName
            Invoke-Native -File 'curl' -Arguments @('-fsSL', "$releaseRoot/$archiveName", '-o', $archivePath)
            Invoke-Native -File 'sha256sum' -Arguments @('-c', 'SHASUMS256.txt', '--ignore-missing') -WorkingDirectory $temporaryDirectory
            Invoke-Root -File 'mkdir' -Arguments @('-p', '/usr/local/lib/nodejs')
            Invoke-Root -File 'tar' -Arguments @('-xJf', $archivePath, '-C', '/usr/local/lib/nodejs')
            $nodeDirectory = "/usr/local/lib/nodejs/$($archiveName.Substring(0, $archiveName.Length - 7))"
            foreach ($command in @('node', 'npm', 'npx', 'corepack')) {
                Invoke-Root -File 'ln' -Arguments @('-sfn', "$nodeDirectory/bin/$command", "/usr/local/bin/$command")
            }
        }
        finally {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $verifiedMajor = [int]((& /usr/bin/env node --version).TrimStart('v').Split('.')[0])
    if ($verifiedMajor -ne $requiredMajor) {
        throw "Node.js installation verification failed: expected $requiredMajor.x, found $verifiedMajor.x."
    }

    if (Test-ConfigProperty -Object $NodeConfig -Name 'packageManager') {
        $manager = [string]$NodeConfig.packageManager.name
        $version = [string]$NodeConfig.packageManager.version
        if (-not $VerifyOnly) {
            Invoke-Root -File 'corepack' -Arguments @('enable')
            Invoke-AsWorker -File 'corepack' -Arguments @('prepare', "$manager@$version", '--activate')
        }
        if (-not (Test-WorkerCommand -Command "command -v $manager >/dev/null 2>&1")) {
            throw "Package manager '$manager' is unavailable for '$WorkerUser'."
        }
    }
}

function Ensure-DotNet {
    param([Parameter(Mandatory)]$DotNetConfig, [Parameter(Mandatory)][string]$RepositoryRoot)

    $globalJsonPath = Join-Path $RepositoryRoot ([string]$DotNetConfig.globalJson)
    if (-not (Test-Path -LiteralPath $globalJsonPath)) {
        throw "Configured global.json was not found: $globalJsonPath"
    }
    $sdkVersion = [string](Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
    $installedSdks = @()
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $installedSdks = @(& dotnet --list-sdks 2>$null | ForEach-Object { ($_ -split '\s+')[0] })
    }
    if ($installedSdks -notcontains $sdkVersion) {
        if ($VerifyOnly) { throw ".NET SDK $sdkVersion is not installed." }
        $installer = Join-Path ([IO.Path]::GetTempPath()) "dotnet-install-$([guid]::NewGuid().ToString('N')).sh"
        try {
            Invoke-Native -File 'curl' -Arguments @('-fsSL', 'https://dot.net/v1/dotnet-install.sh', '-o', $installer)
            Invoke-Root -File 'mkdir' -Arguments @('-p', '/usr/local/share/dotnet')
            Invoke-Root -File 'bash' -Arguments @($installer, '--version', $sdkVersion, '--install-dir', '/usr/local/share/dotnet', '--no-path')
            Invoke-Root -File 'ln' -Arguments @('-sfn', '/usr/local/share/dotnet/dotnet', '/usr/local/bin/dotnet')
        }
        finally {
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
        }
    }

    $verifiedSdks = @(& dotnet --list-sdks 2>$null | ForEach-Object { ($_ -split '\s+')[0] })
    if ($verifiedSdks -notcontains $sdkVersion) {
        throw ".NET SDK installation verification failed: $sdkVersion is unavailable."
    }
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$configPath = Join-Path $PSScriptRoot 'worker.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if ([int]$config.schemaVersion -ne 1) { throw "Unsupported worker schema: $($config.schemaVersion)" }

if (-not (Test-Path -LiteralPath '/etc/os-release')) { throw 'This bootstrap supports Linux workers only.' }
$os = @{}
Get-Content -LiteralPath '/etc/os-release' | ForEach-Object {
    if ($_ -match '^(?<key>[A-Z0-9_]+)=(?<value>.*)$') { $os[$Matches.key] = $Matches.value.Trim('"', "'") }
}
if ($os.ID -ne [string]$config.platform.os -or $os.VERSION_ID.Split('.')[0] -ne [string]$config.platform.majorVersion) {
    throw "Expected $($config.platform.os) $($config.platform.majorVersion); found $($os.ID) $($os.VERSION_ID)."
}
Get-WorkerHome | Out-Null

if (-not $VerifyOnly -and @($config.dependencies.aptPackages).Count -gt 0) {
    Invoke-Root -File 'apt-get' -Arguments @('update')
    Invoke-Root -File 'apt-get' -Arguments (@('install', '-y') + @($config.dependencies.aptPackages))
}
if (Test-ConfigProperty -Object $config.dependencies -Name 'node') {
    Ensure-Node -NodeConfig $config.dependencies.node
}
if (Test-ConfigProperty -Object $config.dependencies -Name 'dotnet') {
    Ensure-DotNet -DotNetConfig $config.dependencies.dotnet -RepositoryRoot $repositoryRoot
}

$hasCodex = Test-WorkerCommand -Command 'command -v codex >/dev/null 2>&1'
if (-not $hasCodex -and $VerifyOnly) { throw "Codex CLI is not installed for '$WorkerUser'." }
if (-not $VerifyOnly -and [bool]$config.dependencies.codexCli.install -and
    (-not $hasCodex -or ([bool]$config.dependencies.codexCli.updateOnBootstrap -and -not $SkipCodexUpdate))) {
    $installer = Join-Path ([IO.Path]::GetTempPath()) "codex-install-$([guid]::NewGuid().ToString('N')).sh"
    try {
        Invoke-Native -File 'curl' -Arguments @('-fsSL', 'https://chatgpt.com/codex/install.sh', '-o', $installer)
        Invoke-Root -File 'chmod' -Arguments @('0755', $installer)
        Invoke-AsWorker -File 'sh' -Arguments @($installer)
    }
    finally {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }
}
if ([bool]$config.dependencies.codexCli.install -and -not (Test-WorkerCommand -Command 'command -v codex >/dev/null 2>&1')) {
    throw "Codex CLI installation verification failed for '$WorkerUser'."
}

foreach ($command in @($config.dependencies.requiredCommands)) {
    if (-not (Test-WorkerCommand -Command "command -v $command >/dev/null 2>&1")) {
        throw "Required command '$command' is unavailable for '$WorkerUser'."
    }
}

$workingDirectory = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot ([string]$config.workingDirectory))).Path
if (-not $VerifyOnly) {
    foreach ($command in @($config.commands.bootstrap)) {
        Invoke-AsWorker -File ([string]$command.file) -Arguments @($command.args) -WorkingDirectory $workingDirectory
    }
    if ($RunTests) {
        foreach ($command in @($config.commands.test)) {
            Invoke-AsWorker -File ([string]$command.file) -Arguments @($command.args) -WorkingDirectory $workingDirectory
        }
    }
}

if (Test-WorkerCommand -Command 'codex login status >/dev/null 2>&1') {
    Write-Host 'CODEX_WORKER_STATUS=ready' -ForegroundColor Green
}
else {
    Write-Warning "Codex authentication is required. Run 'codex login --device-auth' as '$WorkerUser'."
    Write-Host 'CODEX_WORKER_STATUS=ready-needs-codex-auth'
}
