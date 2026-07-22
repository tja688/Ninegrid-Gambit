#Requires -Version 5.1
<#
.SYNOPSIS
  Launch a Unity project with -automated (direct Editor start, no Hub IPC flags).

.DESCRIPTION
  Portable helper for any Unity project. Resolves Editor from
  ProjectSettings/ProjectVersion.txt + `unity editors -i`, starts Unity.exe with
  -projectpath and -automated via ProcessStartInfo.ArgumentList (safe for spaces),
  then waits until com.unity.pipeline reports the project reachable.

.PARAMETER ProjectPath
  Unity project root (contains Assets/ and ProjectSettings/). If omitted: walk up
  from this script for a project root, else use the current directory.

.PARAMETER SkipIfRunning
  If Pipeline already reports this project reachable, exit 0 without relaunching.
  Does NOT verify the existing instance has -automated.

.PARAMETER NoWait
  Start the Editor and return immediately (do not poll Pipeline).

.PARAMETER TimeoutSec
  Max seconds to wait for Pipeline after launch. Default 180.
#>
[CmdletBinding()]
param(
    [string] $ProjectPath = "",
    [switch] $SkipIfRunning,
    [switch] $NoWait,
    [int] $TimeoutSec = 180
)

$ErrorActionPreference = "Stop"

function Test-IsUnityProjectRoot([string] $path) {
    return (Test-Path -LiteralPath (Join-Path $path "ProjectSettings\ProjectVersion.txt")) -and
           (Test-Path -LiteralPath (Join-Path $path "Assets"))
}

function Find-UnityProjectRoot {
    # 1) Walk up from script: .../.cursor/skills/<skill>/scripts -> project root
    $dir = $PSScriptRoot
    for ($i = 0; $i -lt 8 -and $dir; $i++) {
        if (Test-IsUnityProjectRoot $dir) { return (Resolve-Path -LiteralPath $dir).Path }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    # 2) Current working directory
    $cwd = (Get-Location).Path
    if (Test-IsUnityProjectRoot $cwd) { return (Resolve-Path -LiteralPath $cwd).Path }
    return $null
}

function Test-UnityCli {
    $cmd = Get-Command unity -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "unity CLI not found on PATH. Install Unity CLI first."
    }
}

function Get-ProjectEditorVersion([string] $root) {
    $pv = Join-Path $root "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $pv)) {
        throw "ProjectVersion.txt not found: $pv"
    }
    $line = Select-String -LiteralPath $pv -Pattern 'm_EditorVersion:\s*(\S+)' | Select-Object -First 1
    if (-not $line) {
        throw "Could not parse m_EditorVersion from $pv"
    }
    return $line.Matches[0].Groups[1].Value
}

function Get-EditorExe([string] $version) {
    $raw = unity editors -i --format json 2>&1 | Out-String
    $j = $raw | ConvertFrom-Json
    if (-not $j.success) {
        throw "unity editors failed: $raw"
    }
    $match = @($j.data) | Where-Object { $_.version -eq $version } | Select-Object -First 1
    if (-not $match -or -not $match.location) {
        throw "No installed Editor matching version '$version'. Install that Editor, then retry."
    }
    $exe = $match.location
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Editor exe missing: $exe"
    }
    return $exe
}

function Get-PipelineInstance([string] $root) {
    $raw = unity pipeline list --format json 2>&1 | Out-String
    try {
        $j = $raw | ConvertFrom-Json
    } catch {
        return $null
    }
    if (-not $j.success) { return $null }
    $norm = [IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    return @($j.data.instances) | Where-Object {
        if (-not $_.projectPath) { return $false }
        $p = [IO.Path]::GetFullPath($_.projectPath).TrimEnd('\', '/')
        return $p -eq $norm
    } | Select-Object -First 1
}

function Test-PipelineReachable([string] $root) {
    $inst = Get-PipelineInstance $root
    return [bool]($inst -and $inst.pipelineServer -and $inst.pipelineServer.isReachable)
}

# --- main ---
Test-UnityCli

if (-not $ProjectPath) {
    $ProjectPath = Find-UnityProjectRoot
    if (-not $ProjectPath) {
        throw "Pass -ProjectPath to a Unity project root (folder with Assets/ and ProjectSettings/)."
    }
}
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-IsUnityProjectRoot $ProjectPath)) {
    throw "Not a Unity project root: $ProjectPath"
}

Write-Host "Project: $ProjectPath"

if ($SkipIfRunning -and (Test-PipelineReachable $ProjectPath)) {
    $inst = Get-PipelineInstance $ProjectPath
    Write-Host "Pipeline already reachable (pid=$($inst.pid) port=$($inst.pipelineServer.port)). Skipping launch."
    Write-Host "NOTE: -SkipIfRunning does not verify -automated on the existing process."
    exit 0
}

$version = Get-ProjectEditorVersion $ProjectPath
$exe = Get-EditorExe $version
Write-Host "Editor: $version -> $exe"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.UseShellExecute = $false
$psi.WorkingDirectory = $ProjectPath
# ArgumentList is .NET Core / PS7+; on Windows PowerShell 5.1 (.NET Framework) it is null.
if ($null -ne $psi.ArgumentList) {
    $psi.ArgumentList.Add("-projectpath")
    $psi.ArgumentList.Add($ProjectPath)
    $psi.ArgumentList.Add("-automated")
} else {
    $escaped = $ProjectPath.Replace('"', '\"')
    $psi.Arguments = "-projectpath `"$escaped`" -automated"
}

$proc = [Diagnostics.Process]::Start($psi)
if (-not $proc) {
    throw "Failed to start Unity process."
}
Write-Host "Started Unity pid=$($proc.Id) with -automated"

if ($NoWait) {
    exit 0
}

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
    if ($proc.HasExited) {
        throw "Unity exited early (code=$($proc.ExitCode)). Check Editor.log / license."
    }
    if (Test-PipelineReachable $ProjectPath) {
        $inst = Get-PipelineInstance $ProjectPath
        Write-Host "Pipeline reachable: port=$($inst.pipelineServer.port) api=$($inst.pipelineServer.apiUrl)"
        exit 0
    }
    Write-Host ("[{0}] waiting for Pipeline..." -f (Get-Date -Format "HH:mm:ss"))
}

throw @"
Timed out after ${TimeoutSec}s waiting for Pipeline on $ProjectPath.
If the Editor window is open: ensure com.unity.pipeline is installed (unity pipeline install --project-path `"$ProjectPath`").
Or relaunch with -NoWait and diagnose separately.
"@
