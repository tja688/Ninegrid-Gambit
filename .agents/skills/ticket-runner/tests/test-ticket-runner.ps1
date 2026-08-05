#Requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$skill = Join-Path $root ".agents\skills\ticket-runner\SKILL.md"
$reference = Join-Path $root ".agents\skills\ticket-runner\reference.md"
$runner = Join-Path $root ".agents\skills\ticket-runner\scripts\ticket-runner.ps1"
$example = Join-Path $root ".agents\skills\ticket-runner\config.example.json"
$config = Join-Path $root "opencode.json"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "FAIL: $Message" }
}

foreach ($path in @($skill, $reference, $runner, $example, $config)) {
    Assert-True (Test-Path -LiteralPath $path) "missing $path"
}

$skillText = Get-Content -LiteralPath $skill -Raw
$runnerText = Get-Content -LiteralPath $runner -Raw
$configText = Get-Content -LiteralPath $config -Raw

Assert-True ($skillText -match '(?m)^name:\s*ticket-runner\s*$') "skill name must be ticket-runner"
Assert-True ($skillText -match 'opencode run') "skill must document opencode run"
Assert-True ($runnerText -match '--format.*json') "runner must request JSON events"
Assert-True ($runnerText -notmatch '(?<![-\w])--(?:continue|session|fork)(?![-\w])') "runner must not reuse sessions"
Assert-True ($runnerText -match 'ticket-doctor') "runner must have a doctor path"
Assert-True ($runnerText -match 'intervened') "runner must cap intervention"
Assert-True ($runnerText -match 'useFallback') "fallback model must be conditional"
Assert-True ($runnerText -match 'LastStdoutText' -and $runnerText -match 'LastStderrText') "stdout and stderr must be scanned independently"
Assert-True ($runnerText -match 'LastStdoutRemainder' -and $runnerText -match 'LastStderrRemainder') "partial NDJSON lines must be buffered"
Assert-True ($runnerText -match 'Set-RunOutcome') "run outcome must be persisted on every terminal path"
Assert-True ($runnerText -match 'Test-FlagSpecified') "flags without values must be distinguishable from absent flags"
Assert-True ($runnerText -match 'gh auth status') "doctor must check GitHub authentication"
Assert-True ($runnerText -match 'DOCTOR_ACTION' -and $runnerText -match 'Get-OpenCodeText') "doctor must parse JSON event text"
Assert-True ($runnerText -match '--issues must contain only') "malformed explicit issue lists must be rejected"
Assert-True ($runnerText -match 'ready-for-agent') "runner must support the project ready label"
Assert-True ($configText -match 'deepseek/deepseek-v4-flash') "primary model must be canonical DeepSeek V4 Flash"
Assert-True ($configText -match 'alibaba/qwen3\.7-max') "fallback model must be canonical Qwen3.7 Max"
Assert-True ($configText -match '"question"\s*:\s*"deny"') "implementer must not ask interactive questions"
Assert-True ($configText -match '"task"\s*:\s*"deny"') "implementer must not launch subagents"
Assert-True ($configText -match 'git push') "config must deny git push"
Assert-True ($configText -match 'git reset --hard') "config must deny git reset --hard"

Write-Output "PASS: OpenCode ticket-runner contract"
