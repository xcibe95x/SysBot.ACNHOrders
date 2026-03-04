param(
    [string]$InstancesFile = "instances.json",
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

function Is-InstanceEnabled {
    param(
        $EnabledValue
    )

    if ($null -eq $EnabledValue) {
        return $false
    }

    if ($EnabledValue -is [bool]) {
        return [bool]$EnabledValue
    }

    $enabledText = ([string]$EnabledValue).Trim().ToLowerInvariant()
    return @("true", "1", "yes", "y", "on", "enabled") -contains $enabledText
}

function Resolve-AnchorFilename {
    param(
        $AnchorFilenameValue
    )

    $defaultFilename = "anchors"
    if ($null -eq $AnchorFilenameValue -or [string]::IsNullOrWhiteSpace([string]$AnchorFilenameValue)) {
        return $defaultFilename
    }

    $requested = ([string]$AnchorFilenameValue).Trim()
    $leaf = [System.IO.Path]::GetFileName($requested)
    if ([string]::IsNullOrWhiteSpace($leaf)) {
        return $defaultFilename
    }

    return $leaf
}

function Import-AnchorTemplate {
    param(
        [string]$TemplatePath,
        [string]$InstanceDir,
        [string]$InstanceName,
        [bool]$OverwriteExisting,
        [string]$AnchorFilename
    )

    if ([string]::IsNullOrWhiteSpace($TemplatePath)) {
        return
    }

    if (-not (Test-Path $TemplatePath)) {
        Write-Warning "[$InstanceName] anchors template not found: $TemplatePath"
        return
    }

    $destinationPath = Join-Path $InstanceDir $AnchorFilename
    if ((Test-Path $destinationPath) -and (-not $OverwriteExisting)) {
        Write-Host "[$InstanceName] $AnchorFilename already exists; skipping template import"
        return
    }

    Copy-Item -Path $TemplatePath -Destination $destinationPath -Force:$OverwriteExisting
    Write-Host "[$InstanceName] imported anchors template -> $destinationPath"
}

function Ensure-InstanceAnchorFilename {
    param(
        [string]$ConfigPath,
        [string]$InstanceName,
        [string]$AnchorFilename
    )

    if (-not (Test-Path $ConfigPath)) {
        return
    }

    $cfgText = Get-Content -Path $ConfigPath -Raw
    if ([string]::IsNullOrWhiteSpace($cfgText)) {
        return
    }

    try {
        $cfgObj = $cfgText | ConvertFrom-Json
    }
    catch {
        Write-Warning "[$InstanceName] Could not parse config file for anchor update: $ConfigPath"
        return
    }

    if ($null -eq $cfgObj) {
        return
    }

    $desiredAnchorFilename = Resolve-AnchorFilename -AnchorFilenameValue $AnchorFilename
    $anchorProperty = $cfgObj.PSObject.Properties["AnchorFilename"]
    $currentAnchorFilename = if ($anchorProperty) { [string]$anchorProperty.Value } else { "" }

    if ($currentAnchorFilename -eq $desiredAnchorFilename) {
        return
    }

    if ($anchorProperty) {
        $cfgObj.AnchorFilename = $desiredAnchorFilename
    }
    else {
        $cfgObj | Add-Member -MemberType NoteProperty -Name "AnchorFilename" -Value $desiredAnchorFilename
    }

    $cfgObj | ConvertTo-Json -Depth 100 | Set-Content -Path $ConfigPath -Encoding UTF8
    Write-Host "[$InstanceName] set AnchorFilename=$desiredAnchorFilename in $ConfigPath"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$instancesPath = Resolve-FromRoot -BaseDir $repoRoot -PathValue $InstancesFile

if (-not (Test-Path $instancesPath)) {
    $legacyInstancesPath = Resolve-FromRoot -BaseDir $repoRoot -PathValue "scripts/instances.json"
    if (([string]::Equals($InstancesFile, "instances.json", [System.StringComparison]::OrdinalIgnoreCase)) -and (Test-Path $legacyInstancesPath)) {
        Write-Warning "instances.json not found at repo root. Falling back to legacy path: $legacyInstancesPath"
        $instancesPath = $legacyInstancesPath
    }
    else {
        throw "Instances file not found: $instancesPath"
    }
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

$allInstances = @($cfg.instances)
if ($allInstances.Count -eq 0) {
    Write-Host "No instances were defined in $instancesPath"
    exit 0
}

$enabledInstances = @($allInstances | Where-Object { Is-InstanceEnabled $_.enabled })
$disabledInstances = @($allInstances | Where-Object { -not (Is-InstanceEnabled $_.enabled) })

if ($enabledInstances.Count -eq 0) {
    Write-Host "No enabled instances found in $instancesPath"
    exit 0
}

Write-Host "Starting $($enabledInstances.Count) instance(s) from $instancesPath"
if ($disabledInstances.Count -gt 0) {
    $disabledNames = @($disabledInstances | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_.name)) { "unnamed" } else { [string]$_.name }
    }) -join ", "
    Write-Host "Skipping $($disabledInstances.Count) disabled instance(s): $disabledNames"
}

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
    $instanceAnchorTemplatePath = if (-not [string]::IsNullOrWhiteSpace($inst.anchorTemplate)) {
        Resolve-FromRoot -BaseDir $repoRoot -PathValue $inst.anchorTemplate
    }
    else {
        $null
    }
    $instanceOverwriteAnchors = if ($null -ne $inst.overwriteAnchors) {
        Is-InstanceEnabled $inst.overwriteAnchors
    }
    else {
        $false
    }
    $instanceAnchorFilename = Resolve-AnchorFilename -AnchorFilenameValue $inst.anchorFilename
    if ($null -eq $configPath) { $configPath = Join-Path $instDir "config.json" }
    if ($null -eq $twitchPath) { $twitchPath = Join-Path $instDir "twitch.json" }
    if ($null -eq $serverPath) { $serverPath = Join-Path $instDir "server.json" }
    if ($null -eq $extraPath) { $extraPath = Join-Path $instDir "extraconfig.json" }
    if ($null -eq $githubPath) { $githubPath = Join-Path $instDir "github.json" }

    Import-AnchorTemplate -TemplatePath $instanceAnchorTemplatePath -InstanceDir $instDir -InstanceName $name -OverwriteExisting $instanceOverwriteAnchors -AnchorFilename $instanceAnchorFilename
    Ensure-InstanceAnchorFilename -ConfigPath $configPath -InstanceName $name -AnchorFilename $instanceAnchorFilename

    $argList = @()
    if ($runtimeMode -eq "dotnet") {
        $argList += $runtimeTarget
    }
    $argList += @(
        $configPath,
        $twitchPath,
        $serverPath,
        $extraPath,
        $githubPath
    )

    $filePath = if ($runtimeMode -eq "dotnet") { "dotnet" } else { $runtimeTarget }
    $argsLine = (($argList | ForEach-Object { "`"$_`"" }) -join " ")

    Write-Host "[$name] wd=$instDir"
    Write-Host "[$name] $filePath $argsLine"

    if (-not $WhatIf) {
        $p = Start-Process -FilePath $filePath -ArgumentList $argsLine -WorkingDirectory $instDir -PassThru
        Write-Host "[$name] started pid=$($p.Id)"
    }
}

