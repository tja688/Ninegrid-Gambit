# Architecture guardrails for NineGrid.Core (P3/P4).
# 1) Core must not reference UnityEngine.
# 2) Game Systems must not mutate Models outside the Action pipeline.
# Requires: ripgrep (rg) on PATH.

param(
    [string]$ProjectPath = ""
)

$ErrorActionPreference = "Stop"

if (-not $ProjectPath) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
}

function Resolve-Ripgrep {
    if (Get-Command rg -ErrorAction SilentlyContinue) {
        return "rg"
    }

    $cursorRg = Join-Path $env:LOCALAPPDATA "Programs/cursor/resources/app/node_modules/@vscode/ripgrep/bin/rg.exe"
    if (Test-Path $cursorRg) {
        return $cursorRg
    }

    throw "ripgrep (rg) not found on PATH. Install ripgrep or add it to PATH."
}

$rg = Resolve-Ripgrep
$CoreDir = Join-Path $ProjectPath "Assets/Scripts/NineGrid.Core"
$SystemsDir = Join-Path $CoreDir "Systems"
$AsmdefPath = Join-Path $CoreDir "NineGrid.Core.asmdef"

if (-not (Test-Path $CoreDir)) {
    throw "Core directory not found: $CoreDir"
}

$failures = New-Object System.Collections.Generic.List[string]

# --- Guard 1: asmdef noEngineReferences ---
$asmdef = Get-Content $AsmdefPath -Raw
if ($asmdef -notmatch '"noEngineReferences"\s*:\s*true') {
    $failures.Add("NineGrid.Core.asmdef must set `"noEngineReferences`": true")
}

# --- Guard 2: no UnityEngine in Core sources ---
$unityHits = & $rg -n --pcre2 "using\s+UnityEngine\b|global::UnityEngine\b|(?<![A-Za-z0-9_])UnityEngine\." $CoreDir --glob "*.cs" 2>$null
if ($unityHits) {
    foreach ($line in $unityHits) {
        $failures.Add("UnityEngine reference in Core: $line")
    }
}

# --- Guard 3: Systems must not bypass Action pipeline for model mutation ---
$bypassPattern = '\.(PlaceCard|RemoveCard|ClearSlot|ClearBoardCards|SetAvatar|SetBlessed|AddToDrawPile|AddToPlayerCardPool|AddToEnemyCardPool|AddToItemSlots|RemoveUid|ReorderDrawPile|AddCoins|AddInteractionCount|AddRelic|AddSkill|SetPhase|MoveCard)\(|\.Stats\.SetBase\(|\.Counters\.(Set|Add)\(|\.(Zone|Slot)\.Value\s*=|registry\.(Create|Remove|MoveCard)\('

$bypassHits = & $rg -n --pcre2 $bypassPattern $SystemsDir `
    --glob "*.cs" `
    --glob "!ActionPipelineSystem.cs" `
    --glob "!TriggerSystem.cs" `
    --glob "!StatSystem.cs" 2>$null

if ($bypassHits) {
    foreach ($line in $bypassHits) {
        $failures.Add("System bypasses Action pipeline: $line")
    }
}

$modelClearPattern = '(deck|board|registry|player|run)\.(Clear|Reset)\('
$clearHits = & $rg -n --pcre2 $modelClearPattern $SystemsDir `
    --glob "*.cs" `
    --glob "!ActionPipelineSystem.cs" `
    --glob "!TriggerSystem.cs" `
    --glob "!StatSystem.cs" 2>$null

if ($clearHits) {
    foreach ($line in $clearHits) {
        $failures.Add("System clears/resets Model outside Action: $line")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Core architecture guard FAILED ($($failures.Count) issue(s)):" -ForegroundColor Red
    foreach ($item in $failures) {
        Write-Host "  - $item"
    }
    exit 1
}

Write-Host "Core architecture guard passed."
Write-Host "  Core dir : $CoreDir"
Write-Host "  Checks   : noEngineReferences, no UnityEngine, Systems -> Action pipeline"
exit 0
