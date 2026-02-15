param(
    [string]$InstancesFile = "scripts/instances.json",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Resolve-FromRoot {
    param(
        [string]$BaseDir,
        [string]$PathValue
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path $BaseDir $PathValue)
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$instancesPath = Resolve-FromRoot -BaseDir $repoRoot -PathValue $InstancesFile

if (-not (Test-Path $instancesPath)) {
    throw "Instances file not found: $instancesPath"
}

$raw = Get-Content -Path $instancesPath -Raw
$cfg = $raw | ConvertFrom-Json

if ($null -eq $cfg.runtime -or [string]::IsNullOrWhiteSpace($cfg.runtime.mode) -or [string]::IsNullOrWhiteSpace($cfg.runtime.target)) {
    throw "Invalid instances.json: runtime.mode and runtime.target are required."
}

$runtimeMode = "$($cfg.runtime.mode)".ToLowerInvariant()
$runtimeTarget = Resolve-FromRoot -BaseDir $repoRoot -PathValue $cfg.runtime.target
$runtimeWorkDir = Resolve-FromRoot -BaseDir $repoRoot -PathValue $cfg.runtime.workingDirectory
if ([string]::IsNullOrWhiteSpace($runtimeWorkDir)) {
    $runtimeWorkDir = $repoRoot
}

if ($runtimeMode -eq "exe") {
    if (-not (Test-Path $runtimeTarget)) {
        throw "Runtime exe not found: $runtimeTarget"
    }
}
elseif ($runtimeMode -eq "dotnet") {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet is not available in PATH."
    }
    if (-not (Test-Path $runtimeTarget)) {
        throw "Runtime dll not found: $runtimeTarget"
    }
}
else {
    throw "Unsupported runtime.mode '$runtimeMode'. Use 'dotnet' or 'exe'."
}

$enabledInstances = @($cfg.instances | Where-Object { $_.enabled -eq $true })
if ($enabledInstances.Count -eq 0) {
    Write-Host "No enabled instances found in $instancesPath"
    exit 0
}

Write-Host "Starting $($enabledInstances.Count) instance(s) from $instancesPath"

foreach ($inst in $enabledInstances) {
    $name = if ([string]::IsNullOrWhiteSpace($inst.name)) { "unnamed" } else { [string]$inst.name }
    $instDir = Resolve-FromRoot -BaseDir $repoRoot -PathValue $inst.workingDirectory
    if ([string]::IsNullOrWhiteSpace($instDir)) {
        $instDir = $runtimeWorkDir
    }

    if (-not (Test-Path $instDir)) {
        New-Item -Path $instDir -ItemType Directory -Force | Out-Null
    }

    $configPath = Resolve-FromRoot -BaseDir $instDir -PathValue $inst.config
    $twitchPath = Resolve-FromRoot -BaseDir $instDir -PathValue $inst.twitch
    $serverPath = Resolve-FromRoot -BaseDir $instDir -PathValue $inst.server
    $extraPath = Resolve-FromRoot -BaseDir $instDir -PathValue $inst.extra
    $githubPath = Resolve-FromRoot -BaseDir $instDir -PathValue $inst.github

    if ($null -eq $configPath) { $configPath = Join-Path $instDir "config.json" }
    if ($null -eq $twitchPath) { $twitchPath = Join-Path $instDir "twitch.json" }
    if ($null -eq $serverPath) { $serverPath = Join-Path $instDir "server.json" }
    if ($null -eq $extraPath) { $extraPath = Join-Path $instDir "extraconfig.json" }
    if ($null -eq $githubPath) { $githubPath = Join-Path $instDir "github.json" }

    $argList = @()
    if ($runtimeMode -eq "dotnet") {
        $argList += "`"$runtimeTarget`""
    }
    $argList += @(
        "`"$configPath`"",
        "`"$twitchPath`"",
        "`"$serverPath`"",
        "`"$extraPath`"",
        "`"$githubPath`""
    )

    $filePath = if ($runtimeMode -eq "dotnet") { "dotnet" } else { $runtimeTarget }
    $argsLine = ($argList -join " ")

    Write-Host "[$name] wd=$instDir"
    Write-Host "[$name] $filePath $argsLine"

    if (-not $WhatIf) {
        $p = Start-Process -FilePath $filePath -ArgumentList $argsLine -WorkingDirectory $instDir -PassThru
        Write-Host "[$name] started pid=$($p.Id)"
    }
}

