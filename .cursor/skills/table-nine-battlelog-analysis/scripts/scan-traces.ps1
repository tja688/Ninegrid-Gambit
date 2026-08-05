#Requires -Version 5.1
<#
.SYNOPSIS
  Scan TableNine exported trace logs (battlelog/corelog) for Error/Exception/Assert
  and summarize Run/Floor/Node/Battle correlation fields.

.DESCRIPTION
  Reads JSON logs under Assets/Notes/Logs (or -Path). For each file prints:
    - runTag / seed / sessionId
    - floor distribution, node distribution
    - op/event counts
    - Error|Exception|Assert hits with line numbers + snippet

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/table-nine-battlelog-analysis/scripts/scan-traces.ps1"
  powershell ... scan-traces.ps1 -Path "Assets/Notes/Logs"
#>
[CmdletBinding()]
param(
    [string] $Path = "Assets/Notes/Logs"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
if (-not $root) {
    Write-Error "Log dir not found: $Path"
    exit 1
}

$files = Get-ChildItem -LiteralPath $root -Recurse -Filter *.json |
    Where-Object { $_.Name -notlike "_*" -and $_.Name -match "^(battlelog|corelog)" } |
    Sort-Object LastWriteTime

if ($files.Count -eq 0) {
    Write-Host "No battlelog/corelog JSON found under $Path"
    exit 0
}

$pattern = 'Error|Exception|Assert'
$hits = 0

foreach ($file in $files) {
    Write-Host ""
    Write-Host ("==== " + $file.FullName)
    try {
        $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    } catch {
        Write-Host ("  [unparseable JSON] " + $_.Exception.Message)
        continue
    }

    Write-Host ("  runTag='{0}' seed={1} sessionId={2}" -f $json.runTag, $json.seed, $json.sessionId)

    $floors = @{}
    $nodes = @{}
    $count = 0

    if ($null -ne $json.events) {
        foreach ($e in $json.events) {
            $count++
            if ($e.floor) { $floors[$e.floor] = 1 }
            if ($e.nodeIndex) { $nodes[$e.nodeIndex] = 1 }
        }
        $floorKeys = @($floors.Keys) -join ","
        $nodeKeys = @($nodes.Keys) -join ","
        Write-Host ("  events={0} floors={1} nodes={2}" -f $count, $floorKeys, $nodeKeys)
    } elseif ($null -ne $json.ops) {
        foreach ($op in $json.ops) {
            $count++
            if ($op.floor) { $floors[$op.floor] = 1 }
            if ($op.nodeIndex) { $nodes[$op.nodeIndex] = 1 }
        }
        $floorKeys = @($floors.Keys) -join ","
        $nodeKeys = @($nodes.Keys) -join ","
        Write-Host ("  ops={0} floors={1} nodes={2}" -f $count, $floorKeys, $nodeKeys)
    } else {
        Write-Host "  (no events/ops found)"
    }

    $matches = Select-String -LiteralPath $file.FullName -Pattern $pattern -AllMatches
    foreach ($m in $matches) {
        $line = $m.Line.Trim()
        if ($line.Length -gt 200) { $line = $line.Substring(0, 200) }
        Write-Host ("  HIT [{0}:{1}] {2}" -f $file.Name, $m.LineNumber, $line)
        $hits++
    }
}

Write-Host ""
Write-Host ("scan done: {0} hit(s) across {1} file(s)" -f $hits, $files.Count)
if ($hits -gt 0) { exit 3 }
exit 0
