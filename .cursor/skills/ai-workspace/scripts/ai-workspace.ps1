#Requires -Version 5.1
<#
.SYNOPSIS
  Lightweight multi-AI workspace coordinator: claims + EditMode test lock.

.DESCRIPTION
  Coordinates multiple agents on one Unity project root:
  - Multi claim (1A): others' live claims block aggressive Editor restart.
  - EditMode test mutex (2A): wraps `unity command run_tests --mode editor`;
    default wait-and-share when busy; --no-wait fails fast.

.NOTES
  Exit codes:
    0  success
    1  usage / internal error
    2  gate denied or --no-wait while lock busy
    3  Unity / test failure (including shared failed result)
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Command = "",

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest = @()
)

$ErrorActionPreference = "Stop"
$script:ClaimTtlMinutes = 30
$script:LastResultTtlMinutes = 30
$script:LockHardTtlHours = 2
$script:PollSeconds = 3

# ---------------------------------------------------------------------------
# Paths / project root
# ---------------------------------------------------------------------------

function Test-IsUnityProjectRoot([string] $path) {
    return (Test-Path -LiteralPath (Join-Path $path "ProjectSettings\ProjectVersion.txt")) -and
           (Test-Path -LiteralPath (Join-Path $path "Assets"))
}

function Find-UnityProjectRoot {
    $dir = $PSScriptRoot
    for ($i = 0; $i -lt 8 -and $dir; $i++) {
        if (Test-IsUnityProjectRoot $dir) { return (Resolve-Path -LiteralPath $dir).Path }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    $cwd = (Get-Location).Path
    if (Test-IsUnityProjectRoot $cwd) { return (Resolve-Path -LiteralPath $cwd).Path }
    return $null
}

function Get-StateDir([string] $projectRoot) {
    return Join-Path $projectRoot ".cursor\ai-workspace"
}

function Get-StatePath([string] $projectRoot) {
    return Join-Path (Get-StateDir $projectRoot) "state.json"
}

function Get-LogsDir([string] $projectRoot) {
    return Join-Path (Get-StateDir $projectRoot) "logs"
}

function Ensure-StateDir([string] $projectRoot) {
    $dir = Get-StateDir $projectRoot
    $logs = Get-LogsDir $projectRoot
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $logs)) {
        New-Item -ItemType Directory -Path $logs -Force | Out-Null
    }
}

function Get-IsoNow {
    return (Get-Date).ToUniversalTime().ToString("o")
}

function ConvertFrom-Iso([string] $iso) {
    if ([string]::IsNullOrWhiteSpace($iso)) { return $null }
    try { return [DateTimeOffset]::Parse($iso).UtcDateTime } catch { return $null }
}

function Test-ProcessAlive([int] $ProcessId) {
    if ($ProcessId -le 0) { return $false }
    try {
        $p = Get-Process -Id $ProcessId -ErrorAction Stop
        return $null -ne $p
    } catch {
        return $false
    }
}

function New-EmptyState {
    return @{
        claims       = @{}
        editModeTest = @{
            lock = $null
            last = $null
        }
    }
}

function ConvertTo-Map($obj) {
    if ($null -eq $obj) { return $null }
    if ($obj -is [hashtable]) {
        $copy = @{}
        foreach ($k in @($obj.Keys)) { $copy["$k"] = ConvertTo-Map $obj[$k] }
        return $copy
    }
    if ($obj -is [System.Collections.IDictionary]) {
        $copy = @{}
        foreach ($k in @($obj.Keys)) { $copy["$k"] = ConvertTo-Map $obj[$k] }
        return $copy
    }
    # JSON objects become PSCustomObject
    if ($obj -is [System.Management.Automation.PSCustomObject]) {
        $copy = @{}
        foreach ($p in $obj.PSObject.Properties) {
            $copy[$p.Name] = ConvertTo-Map $p.Value
        }
        return $copy
    }
    return $obj
}

function Normalize-State($raw) {
    $empty = New-EmptyState
    if ($null -eq $raw) { return $empty }

    $h = ConvertTo-Map $raw
    if ($h -isnot [hashtable]) { return $empty }

    $claims = @{}
    if ($h.ContainsKey("claims") -and $null -ne $h.claims) {
        $rawClaims = ConvertTo-Map $h.claims
        if ($rawClaims -is [hashtable]) {
            foreach ($key in @($rawClaims.Keys)) {
                if ([string]::IsNullOrWhiteSpace("$key")) { continue }
                $c = ConvertTo-Map $rawClaims[$key]
                if ($c -is [hashtable]) { $claims["$key"] = $c }
            }
        }
    }

    $emt = @{ lock = $null; last = $null }
    if ($h.ContainsKey("editModeTest") -and $null -ne $h.editModeTest) {
        $rawEmt = ConvertTo-Map $h.editModeTest
        if ($rawEmt -is [hashtable]) {
            if ($rawEmt.ContainsKey("lock") -and $null -ne $rawEmt.lock) {
                $lockMap = ConvertTo-Map $rawEmt.lock
                if ($lockMap -is [hashtable]) { $emt.lock = $lockMap }
            }
            if ($rawEmt.ContainsKey("last") -and $null -ne $rawEmt.last) {
                $lastMap = ConvertTo-Map $rawEmt.last
                if ($lastMap -is [hashtable]) { $emt.last = $lastMap }
            }
        }
    }

    return @{
        claims       = $claims
        editModeTest = $emt
    }
}

function Read-StateUnlocked([string] $statePath) {
    if (-not (Test-Path -LiteralPath $statePath)) {
        return New-EmptyState
    }
    $text = [IO.File]::ReadAllText($statePath)
    if ([string]::IsNullOrWhiteSpace($text)) {
        return New-EmptyState
    }
    $obj = $text | ConvertFrom-Json
    return Normalize-State $obj
}

function Write-StateUnlocked([string] $statePath, $state) {
    $dir = Split-Path -Parent $statePath
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = ConvertTo-Json -InputObject $state -Depth 10
    $tmp = "$statePath.tmp"
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [IO.File]::WriteAllText($tmp, $json, $utf8NoBom)
    if (Test-Path -LiteralPath $statePath) {
        Remove-Item -LiteralPath $statePath -Force
    }
    Move-Item -LiteralPath $tmp -Destination $statePath -Force
}

function Invoke-WithStateLock {
    param(
        [string] $projectRoot,
        [scriptblock] $Action
    )
    Ensure-StateDir $projectRoot
    $statePath = Get-StatePath $projectRoot
    $lockPath = "$statePath.lock"
    $fs = $null
    $deadline = (Get-Date).AddSeconds(30)
    while ($true) {
        try {
            $fs = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            break
        } catch {
            if ((Get-Date) -ge $deadline) {
                throw "Timed out acquiring state file lock: $lockPath"
            }
            Start-Sleep -Milliseconds 100
        }
    }
    try {
        $state = Read-StateUnlocked $statePath
        $result = & $Action $state
        # Action may mutate $state in place; always rewrite unless it returned a replacement hashtable via tuple
        if ($result -is [hashtable] -and $result.ContainsKey("__state")) {
            Write-StateUnlocked $statePath $result["__state"]
            return $result["__value"]
        }
        Write-StateUnlocked $statePath $state
        return $result
    } finally {
        if ($null -ne $fs) {
            $fs.Close()
            $fs.Dispose()
        }
    }
}

function Sweep-Stale($state) {
    $now = (Get-Date).ToUniversalTime()
    $claimCutoff = $now.AddMinutes(-$script:ClaimTtlMinutes)
    $toRemove = @()
    foreach ($key in @($state.claims.Keys)) {
        $c = $state.claims[$key]
        $hb = ConvertFrom-Iso ([string]$c.heartbeatAt)
        $staleHb = ($null -eq $hb) -or ($hb -lt $claimCutoff)
        $pidAlive = $false
        if ($c.ContainsKey("pid")) { $pidAlive = Test-ProcessAlive ([int]$c.pid) }
        # Stale if heartbeat expired; also drop if pid dead AND heartbeat older than half TTL
        if ($staleHb) {
            $toRemove += $key
        } elseif (-not $pidAlive -and $null -ne $hb -and $hb -lt $now.AddMinutes(-([math]::Max(5, $script:ClaimTtlMinutes / 2)))) {
            $toRemove += $key
        }
    }
    foreach ($k in $toRemove) { $state.claims.Remove($k) }

    $lock = $state.editModeTest.lock
    if ($null -ne $lock) {
        $started = ConvertFrom-Iso ([string]$lock.startedAt)
        $hardCut = $now.AddHours(-$script:LockHardTtlHours)
        $pidOk = $false
        if ($lock.ContainsKey("pid")) { $pidOk = Test-ProcessAlive ([int]$lock.pid) }
        $tooOld = ($null -eq $started) -or ($started -lt $hardCut)
        if ((-not $pidOk) -or $tooOld) {
            $state.editModeTest.lock = $null
        }
    }

    $last = $state.editModeTest.last
    if ($null -ne $last) {
        $fin = ConvertFrom-Iso ([string]$last.finishedAt)
        $lastCut = $now.AddMinutes(-$script:LastResultTtlMinutes)
        if ($null -eq $fin -or $fin -lt $lastCut) {
            $state.editModeTest.last = $null
        }
    }
}

function Normalize-Filter([string] $filter) {
    if ([string]::IsNullOrWhiteSpace($filter)) { return "" }
    return $filter.Trim()
}

function Get-OtherLiveClaims($state, [string] $selfAgent) {
    $others = @()
    foreach ($key in @($state.claims.Keys)) {
        if ($key -eq $selfAgent) { continue }
        $others += $state.claims[$key]
    }
    return $others
}

# ---------------------------------------------------------------------------
# Arg parsing helpers
# ---------------------------------------------------------------------------

function Test-HelpFlag([string[]] $argsList) {
    foreach ($a in $argsList) {
        if ($a -eq "-h" -or $a -eq "--help" -or $a -eq "-Help" -or $a -eq "/?") { return $true }
    }
    return $false
}

function Get-FlagValue {
    param(
        [string[]] $ArgsList,
        [string[]] $Names,
        [switch] $AsSwitch
    )
    for ($i = 0; $i -lt $ArgsList.Count; $i++) {
        $a = $ArgsList[$i]
        foreach ($n in $Names) {
            if ($a -eq $n) {
                if ($AsSwitch) { return $true }
                if ($i + 1 -ge $ArgsList.Count) { throw "Missing value for $n" }
                return $ArgsList[$i + 1]
            }
            if ($a.StartsWith("$n=")) {
                if ($AsSwitch) { return $true }
                return $a.Substring($n.Length + 1)
            }
        }
    }
    if ($AsSwitch) { return $false }
    return $null
}

function Remove-KnownFlags {
    param(
        [string[]] $ArgsList,
        [string[]] $FlagNames,
        [string[]] $SwitchNames
    )
    $out = New-Object System.Collections.Generic.List[string]
    $i = 0
    while ($i -lt $ArgsList.Count) {
        $a = $ArgsList[$i]
        $consumed = $false
        foreach ($n in $FlagNames) {
            if ($a -eq $n) {
                $i += 2
                $consumed = $true
                break
            }
            if ($a.StartsWith("$n=")) {
                $i += 1
                $consumed = $true
                break
            }
        }
        if ($consumed) { continue }
        foreach ($n in $SwitchNames) {
            if ($a -eq $n) {
                $i += 1
                $consumed = $true
                break
            }
        }
        if ($consumed) { continue }
        if ($a -eq "--") {
            $i++
            while ($i -lt $ArgsList.Count) {
                $out.Add($ArgsList[$i])
                $i++
            }
            break
        }
        $out.Add($a)
        $i++
    }
    return ,$out.ToArray()
}

function Resolve-ProjectRoot([string[]] $argsList) {
    $pp = Get-FlagValue -ArgsList $argsList -Names @("--project-path", "-ProjectPath")
    if ($pp) {
        $root = [IO.Path]::GetFullPath($pp)
        if (-not (Test-IsUnityProjectRoot $root)) {
            throw "Not a Unity project root: $root"
        }
        return $root
    }
    $root = Find-UnityProjectRoot
    if (-not $root) {
        throw "Pass --project-path to a Unity project root (folder with Assets/ and ProjectSettings/)."
    }
    return $root
}

function Require-Agent([string[]] $argsList) {
    $agent = Get-FlagValue -ArgsList $argsList -Names @("--agent", "-Agent")
    if ([string]::IsNullOrWhiteSpace($agent)) {
        throw "Missing required --agent <id>"
    }
    return $agent.Trim()
}

# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------

function Show-Help {
    Write-Host @"
ai-workspace — multi-AI coordinator for one Unity project (claims + EditMode test lock)

USAGE
  powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/ai-workspace/scripts/ai-workspace.ps1" <command> [options]

COMMANDS
  status              Show live claims + EditMode test lock / last result
  claim               Register or refresh this AI's workspace claim (heartbeat)
  release             Drop this AI's claim
  gate-restart        Exit 0 if restart OK; exit 2 if other agents still claimed
  test                Run EditMode tests under mutex (default: wait + share)
  test-status         Show only EditMode test lock / last result
  help                Show this help

GLOBAL OPTIONS
  --project-path <dir>   Unity project root (default: walk up from script / cwd)
  --json                 Machine-readable JSON on stdout (where applicable)
  -h, --help             Show help (also works after a subcommand)

SEMANTICS
  Claims (1A): multiple agents may claim at once. Aggressive Editor ops
  (quit/relaunch) are forbidden while ANY OTHER live claim exists.
  Use gate-restart before unity-automated-launch.

  EditMode test (2A): only one runner at a time. If busy, default waits for
  the in-flight run and shares its result when --filter matches. Pass
  --no-wait to fail immediately with owner info.

EXIT CODES
  0  success
  1  usage / internal error
  2  gate denied, or test --no-wait while lock busy
  3  Unity / test failure (shared failed results also use 3; look for shared=true)

STATE
  <project>/.cursor/ai-workspace/state.json   (gitignored)
  <project>/.cursor/ai-workspace/logs/        test stdout/stderr logs

EXAMPLES
  ... ai-workspace.ps1 claim --agent cursor-a --note "issue 42"
  ... ai-workspace.ps1 status
  ... ai-workspace.ps1 gate-restart --agent cursor-a
  ... ai-workspace.ps1 test --agent cursor-a --filter MyNamespace
  ... ai-workspace.ps1 test --agent cursor-b --filter MyNamespace --no-wait
  ... ai-workspace.ps1 release --agent cursor-a

TTL
  claim heartbeat: $($script:ClaimTtlMinutes) min
  last test result share window: $($script:LastResultTtlMinutes) min
  test lock hard TTL: $($script:LockHardTtlHours) h (or holder pid dead)
"@
}

function Show-CommandHelp([string] $cmd) {
    $text = switch ($cmd) {
        "status" {
            @"
status — show live claims and EditMode test lock/last result

USAGE
  ai-workspace.ps1 status [--project-path <dir>] [--json]
"@
        }
        "claim" {
            @"
claim — register or refresh this AI's workspace claim

USAGE
  ai-workspace.ps1 claim --agent <id> [--note <text>] [--project-path <dir>] [--json]

NOTES
  Re-run periodically on long tasks to refresh heartbeat (TTL $($script:ClaimTtlMinutes) min).
"@
        }
        "release" {
            @"
release — drop this AI's workspace claim

USAGE
  ai-workspace.ps1 release --agent <id> [--project-path <dir>] [--json]
"@
        }
        "gate-restart" {
            @"
gate-restart — allow aggressive Editor restart only if no OTHER live claims

USAGE
  ai-workspace.ps1 gate-restart --agent <id> [--project-path <dir>] [--json]

EXIT
  0  safe to quit/relaunch Editor (only self claimed, or no claims)
  2  other agents still claimed — do NOT restart
"@
        }
        "test" {
            @"
test — run Unity EditMode tests under a workspace mutex

USAGE
  ai-workspace.ps1 test --agent <id> [--filter <name>] [--no-wait] [--wait-timeout-sec N]
                        [--project-path <dir>] [--json] [-- <extra unity args>]

BEHAVIOR
  Acquires editModeTest lock, runs:
    unity command run_tests --mode editor [--filter ...] --project-path <root> --format json
  Releases lock and stores last result for waiters.

  If lock held:
    default     wait until done; if last.filter matches, share result (no re-run)
    --no-wait   exit 2 immediately with owner info

OPTIONS
  --filter <name>         Passed to unity run_tests (empty = full EditMode suite)
  --no-wait               Fail fast when busy
  --wait-timeout-sec N    Max wait seconds (default 7200)
  --                      Extra args forwarded to unity command run_tests
"@
        }
        "test-status" {
            @"
test-status — show EditMode test lock and last shared result only

USAGE
  ai-workspace.ps1 test-status [--project-path <dir>] [--json]
"@
        }
        default { $null }
    }
    if ($null -eq $text) { Show-Help }
    else { Write-Host $text }
}

# ---------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------

function Write-HumanStatus($state, [string] $projectRoot) {
    Write-Host "project: $projectRoot"
    Write-Host "state:   $(Get-StatePath $projectRoot)"
    Write-Host ""
    Write-Host "claims:"
    if ($state.claims.Count -eq 0) {
        Write-Host "  (none)"
    } else {
        foreach ($key in ($state.claims.Keys | Sort-Object)) {
            $c = $state.claims[$key]
            $note = if ($c.note) { " note=$($c.note)" } else { "" }
            Write-Host "  - $($c.agent) pid=$($c.pid) heartbeat=$($c.heartbeatAt)$note"
        }
    }
    Write-Host ""
    Write-Host "editModeTest.lock:"
    if ($null -eq $state.editModeTest.lock) {
        Write-Host "  (free)"
    } else {
        $L = $state.editModeTest.lock
        $f = if ($L.filter) { $L.filter } else { "(all)" }
        Write-Host "  agent=$($L.agent) pid=$($L.pid) filter=$f started=$($L.startedAt)"
        if ($L.logPath) { Write-Host "  log=$($L.logPath)" }
    }
    Write-Host ""
    Write-Host "editModeTest.last:"
    if ($null -eq $state.editModeTest.last) {
        Write-Host "  (none)"
    } else {
        $R = $state.editModeTest.last
        $f = if ($R.filter) { $R.filter } else { "(all)" }
        Write-Host "  agent=$($R.agent) filter=$f exitCode=$($R.exitCode) finished=$($R.finishedAt)"
        if ($R.summary) { Write-Host "  summary=$($R.summary)" }
        if ($R.resultPath) { Write-Host "  result=$($R.resultPath)" }
    }
}

function Emit-Json($obj) {
    $json = ConvertTo-Json -InputObject $obj -Depth 10
    [Console]::Out.WriteLine($json)
}

# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

function Invoke-Status([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "status"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    $state = Invoke-WithStateLock $root {
        param($s)
        Sweep-Stale $s
        # return copy-ish snapshot via mutation only; caller reads $s after
        $null
    }
    # Re-read after sweep written
    $state = Read-StateUnlocked (Get-StatePath $root)
    if ($asJson) {
        Emit-Json @{
            project = $root
            statePath = (Get-StatePath $root)
            claims = $state.claims
            editModeTest = $state.editModeTest
        }
    } else {
        Write-HumanStatus $state $root
    }
    return 0
}

function Invoke-Claim([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "claim"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $agent = Require-Agent $argsList
    $note = Get-FlagValue -ArgsList $argsList -Names @("--note", "-Note")
    if ($null -eq $note) { $note = "" }
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    $now = Get-IsoNow
    $agentPid = $PID
    $claim = Invoke-WithStateLock $root {
        param($s)
        Sweep-Stale $s
        $existing = $null
        if ($s.claims.ContainsKey($agent)) { $existing = $s.claims[$agent] }
        $claimedAt = $now
        if ($null -ne $existing -and $existing.claimedAt) { $claimedAt = $existing.claimedAt }
        $entry = @{
            agent       = $agent
            pid         = $agentPid
            note        = $note
            claimedAt   = $claimedAt
            heartbeatAt = $now
        }
        $s.claims[$agent] = $entry
        return $entry
    }
    if ($asJson) {
        Emit-Json @{ ok = $true; claim = $claim }
    } else {
        Write-Host "claimed agent=$agent pid=$agentPid heartbeat=$now"
        if ($note) { Write-Host "note=$note" }
    }
    return 0
}

function Invoke-Release([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "release"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $agent = Require-Agent $argsList
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    $removed = Invoke-WithStateLock $root {
        param($s)
        Sweep-Stale $s
        $had = $s.claims.ContainsKey($agent)
        if ($had) { $s.claims.Remove($agent) }
        return $had
    }
    if ($asJson) {
        Emit-Json @{ ok = $true; released = [bool]$removed; agent = $agent }
    } else {
        if ($removed) { Write-Host "released agent=$agent" }
        else { Write-Host "no claim for agent=$agent (ok)" }
    }
    return 0
}

function Invoke-GateRestart([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "gate-restart"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $agent = Require-Agent $argsList
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    $others = Invoke-WithStateLock $root {
        param($s)
        Sweep-Stale $s
        return ,(Get-OtherLiveClaims $s $agent)
    }
    # re-read after sweep for accurate others (Get-OtherLiveClaims returned from lock)
    $state = Read-StateUnlocked (Get-StatePath $root)
    $others = Get-OtherLiveClaims $state $agent
    if ($others.Count -gt 0) {
        $names = @($others | ForEach-Object { $_.agent }) -join ", "
        if ($asJson) {
            Emit-Json @{
                ok = $false
                allowed = $false
                reason = "other_claims"
                others = @($others)
            }
        } else {
            Write-Host "gate-restart DENIED: other live claims: $names"
            foreach ($o in $others) {
                Write-Host "  - $($o.agent) pid=$($o.pid) heartbeat=$($o.heartbeatAt) note=$($o.note)"
            }
            Write-Host "Do NOT quit/relaunch Unity Editor until they release (or claims expire)."
        }
        return 2
    }
    if ($asJson) {
        Emit-Json @{ ok = $true; allowed = $true }
    } else {
        Write-Host "gate-restart OK: no other live claims (agent=$agent)"
    }
    return 0
}

function Get-TestSummaryFromLog([string] $logPath, [int] $exitCode) {
    if (-not (Test-Path -LiteralPath $logPath)) {
        return "exitCode=$exitCode (no log)"
    }
    $raw = [IO.File]::ReadAllText($logPath)
    # Prefer Unity Test Framework Summary fields if present
    $total = [regex]::Match($raw, '"Total"\s*:\s*(\d+)')
    $passed = [regex]::Match($raw, '"Passed"\s*:\s*(\d+)')
    $failed = [regex]::Match($raw, '"Failed"\s*:\s*(\d+)')
    if ($total.Success -and $passed.Success -and $failed.Success) {
        return ("exitCode={0}; Total={1} Passed={2} Failed={3}" -f $exitCode, $total.Groups[1].Value, $passed.Groups[1].Value, $failed.Groups[1].Value)
    }
    $tail = Get-Content -LiteralPath $logPath -Tail 20 -ErrorAction SilentlyContinue
    if (-not $tail) { return "exitCode=$exitCode" }
    $hit = $tail | Where-Object { $_ -match 'pass|fail|Status|Total' } | Select-Object -Last 2
    if ($hit) {
        $line = ($hit -join " | ")
        return $line.Substring(0, [Math]::Min(240, $line.Length))
    }
    return "exitCode=$exitCode"
}

function Invoke-UnityRunTests {
    param(
        [string] $projectRoot,
        [string] $filter,
        [string[]] $extraArgs,
        [string] $logPath
    )
    $unity = Get-Command unity -ErrorAction SilentlyContinue
    if (-not $unity) { throw "unity CLI not found on PATH" }

    $argList = @(
        "command", "run_tests",
        "--mode", "editor",
        "--project-path", $projectRoot,
        "--format", "json"
    )
    if (-not [string]::IsNullOrWhiteSpace($filter)) {
        $argList += @("--filter", $filter)
    }
    if ($extraArgs -and $extraArgs.Count -gt 0) {
        $argList += $extraArgs
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $unity.Source
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = $projectRoot

    if ($null -ne $psi.ArgumentList) {
        foreach ($a in $argList) { [void]$psi.ArgumentList.Add($a) }
    } else {
        $escaped = @()
        foreach ($a in $argList) {
            if ($a -match '[\s"]') { $escaped += ('"{0}"' -f ($a -replace '"', '\"')) }
            else { $escaped += $a }
        }
        $psi.Arguments = ($escaped -join " ")
    }

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $stdout = New-Object System.Text.StringBuilder
    $stderr = New-Object System.Text.StringBuilder
    $outHandler = {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            [void]$Event.MessageData.AppendLine($EventArgs.Data)
        }
    }
    $errHandler = {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            [void]$Event.MessageData.AppendLine($EventArgs.Data)
        }
    }
    $outEvent = Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -Action $outHandler -MessageData $stdout
    $errEvent = Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -Action $errHandler -MessageData $stderr
    try {
        [void]$proc.Start()
        $proc.BeginOutputReadLine()
        $proc.BeginErrorReadLine()
        $proc.WaitForExit()
        $code = $proc.ExitCode
    } finally {
        Unregister-Event -SourceIdentifier $outEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $errEvent.Name -ErrorAction SilentlyContinue
        if ($outEvent) { Remove-Job $outEvent.Id -Force -ErrorAction SilentlyContinue }
        if ($errEvent) { Remove-Job $errEvent.Id -Force -ErrorAction SilentlyContinue }
        if (-not $proc.HasExited) { try { $proc.Kill() } catch {} }
        $proc.Dispose()
    }

    $text = "=== stdout ===`r`n" + $stdout.ToString() + "`r`n=== stderr ===`r`n" + $stderr.ToString()
    [IO.File]::WriteAllText($logPath, $text)
    # Also stream a short echo for the caller
    Write-Host $stdout.ToString().TrimEnd()
    if ($stderr.Length -gt 0) {
        [Console]::Error.WriteLine($stderr.ToString().TrimEnd())
    }
    return $code
}

function Write-SharedResult($last, [switch] $AsJson) {
    if ($AsJson) {
        Emit-Json @{
            ok = ($last.exitCode -eq 0)
            shared = $true
            result = $last
        }
    } else {
        Write-Host "shared=true agent=$($last.agent) filter=$(if ($last.filter) { $last.filter } else { '(all)' }) exitCode=$($last.exitCode)"
        if ($last.summary) { Write-Host "summary=$($last.summary)" }
        if ($last.resultPath) { Write-Host "result=$($last.resultPath)" }
    }
    if ([int]$last.exitCode -eq 0) { return 0 }
    return 3
}

function Invoke-Test([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "test"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $agent = Require-Agent $argsList
    $filter = Normalize-Filter (Get-FlagValue -ArgsList $argsList -Names @("--filter", "-Filter"))
    if ($null -eq $filter) { $filter = "" }
    $noWait = [bool](Get-FlagValue -ArgsList $argsList -Names @("--no-wait", "-NoWait") -AsSwitch)
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    $waitTimeout = Get-FlagValue -ArgsList $argsList -Names @("--wait-timeout-sec")
    if (-not $waitTimeout) { $waitTimeout = 7200 }
    $waitTimeout = [int]$waitTimeout

    $extra = Remove-KnownFlags -ArgsList $argsList -FlagNames @(
        "--project-path", "-ProjectPath", "--agent", "-Agent", "--filter", "-Filter",
        "--wait-timeout-sec", "--note", "-Note"
    ) -SwitchNames @("--json", "--no-wait", "-NoWait", "-h", "--help", "-Help", "/?")

    Ensure-StateDir $root
    $acquired = $false
    $logPath = $null
    $generation = $null

    try {
        # Try acquire or wait/share
        while ($true) {
            $decision = Invoke-WithStateLock $root {
                param($s)
                Sweep-Stale $s
                $lock = $s.editModeTest.lock
                if ($null -eq $lock) {
                    # Check if a matching last result just appeared (another waiter already consumed)
                    # Acquire
                    $ts = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
                    $safeAgent = ($agent -replace '[^\w\-]+', '_')
                    $logPathLocal = Join-Path (Get-LogsDir $root) ("test-{0}-{1}.log" -f $ts, $safeAgent)
                    $gen = [guid]::NewGuid().ToString("N")
                    $s.editModeTest.lock = @{
                        agent     = $agent
                        pid       = $PID
                        filter    = $filter
                        startedAt = (Get-IsoNow)
                        logPath   = $logPathLocal
                        generation = $gen
                    }
                    return @{ action = "run"; logPath = $logPathLocal; generation = $gen }
                }
                # Busy
                if ($noWait) {
                    return @{ action = "busy"; lock = $lock }
                }
                return @{ action = "wait"; lock = $lock }
            }

            if ($decision.action -eq "busy") {
                $L = $decision.lock
                if ($asJson) {
                    Emit-Json @{ ok = $false; busy = $true; lock = $L }
                } else {
                    Write-Host "editModeTest BUSY agent=$($L.agent) pid=$($L.pid) filter=$(if ($L.filter) { $L.filter } else { '(all)' }) started=$($L.startedAt)"
                }
                return 2
            }

            if ($decision.action -eq "run") {
                $acquired = $true
                $logPath = $decision.logPath
                $generation = $decision.generation
                break
            }

            # wait
            $L = $decision.lock
            if (-not $asJson) {
                Write-Host ("waiting for editModeTest lock holder={0} filter={1} ..." -f $L.agent, $(if ($L.filter) { $L.filter } else { "(all)" }))
            }
            $deadline = (Get-Date).AddSeconds($waitTimeout)
            $sawRelease = $false
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Seconds $script:PollSeconds
                $snap = Invoke-WithStateLock $root {
                    param($s)
                    Sweep-Stale $s
                    return @{
                        lock = $s.editModeTest.lock
                        last = $s.editModeTest.last
                    }
                }
                if ($null -ne $snap.lock) { continue }
                $sawRelease = $true
                $last = $snap.last
                if ($null -ne $last) {
                    $lastFilter = Normalize-Filter ([string]$last.filter)
                    $fin = ConvertFrom-Iso ([string]$last.finishedAt)
                    $fresh = ($null -ne $fin) -and ($fin -gt (Get-Date).ToUniversalTime().AddMinutes(-$script:LastResultTtlMinutes))
                    if ($fresh -and ($lastFilter -eq $filter)) {
                        return (Write-SharedResult $last -AsJson:$asJson)
                    }
                }
                break
            }
            if (-not $sawRelease -and (Get-Date) -ge $deadline) {
                throw "Timed out after ${waitTimeout}s waiting for editModeTest lock"
            }
            # Loop to try acquire (or share already handled)
        }

        if (-not $asJson) {
            Write-Host "editModeTest RUN agent=$agent filter=$(if ($filter) { $filter } else { '(all)' }) log=$logPath"
        }

        $exitCode = 3
        $aborted = $false
        try {
            $exitCode = Invoke-UnityRunTests -projectRoot $root -filter $filter -extraArgs $extra -logPath $logPath
        } catch {
            $aborted = $true
            $msg = "$_"
            if ($logPath) {
                try { [IO.File]::AppendAllText($logPath, "`r`n=== exception ===`r`n$msg`r`n") } catch {}
            }
            Write-Error $msg
            $exitCode = 3
        }

        $summary = Get-TestSummaryFromLog $logPath $exitCode
        if ($aborted) { $summary = "aborted: $summary" }

        Invoke-WithStateLock $root {
            param($s)
            Sweep-Stale $s
            $cur = $s.editModeTest.lock
            if ($null -ne $cur -and $cur.generation -eq $generation) {
                $s.editModeTest.lock = $null
            } elseif ($null -ne $cur -and $cur.agent -eq $agent -and $cur.pid -eq $PID) {
                $s.editModeTest.lock = $null
            }
            $s.editModeTest.last = @{
                agent      = $agent
                filter     = $filter
                finishedAt = (Get-IsoNow)
                exitCode   = [int]$exitCode
                summary    = $summary
                resultPath = $logPath
                shared     = $false
            }
            return $null
        }
        $acquired = $false

        if ($asJson) {
            Emit-Json @{
                ok = ($exitCode -eq 0)
                shared = $false
                exitCode = $exitCode
                filter = $filter
                resultPath = $logPath
                summary = $summary
            }
        } else {
            Write-Host "editModeTest DONE shared=false exitCode=$exitCode summary=$summary"
            Write-Host "result=$logPath"
        }
        if ($exitCode -eq 0) { return 0 }
        return 3
    } catch {
        if ($acquired) {
            try {
                Invoke-WithStateLock $root {
                    param($s)
                    $cur = $s.editModeTest.lock
                    if ($null -ne $cur -and (
                            ($generation -and $cur.generation -eq $generation) -or
                            ($cur.agent -eq $agent -and $cur.pid -eq $PID)
                        )) {
                        $s.editModeTest.lock = $null
                        $s.editModeTest.last = @{
                            agent      = $agent
                            filter     = $filter
                            finishedAt = (Get-IsoNow)
                            exitCode   = 3
                            summary    = "aborted: $_"
                            resultPath = $logPath
                            shared     = $false
                        }
                    }
                    return $null
                }
            } catch {}
        }
        throw
    }
}

function Invoke-TestStatus([string[]] $argsList) {
    if (Test-HelpFlag $argsList) { Show-CommandHelp "test-status"; return 0 }
    $root = Resolve-ProjectRoot $argsList
    $asJson = [bool](Get-FlagValue -ArgsList $argsList -Names @("--json") -AsSwitch)
    Invoke-WithStateLock $root {
        param($s)
        Sweep-Stale $s
        $null
    } | Out-Null
    $state = Read-StateUnlocked (Get-StatePath $root)
    if ($asJson) {
        Emit-Json @{
            project = $root
            editModeTest = $state.editModeTest
        }
    } else {
        Write-Host "project: $root"
        Write-Host "editModeTest.lock:"
        if ($null -eq $state.editModeTest.lock) {
            Write-Host "  (free)"
        } else {
            $L = $state.editModeTest.lock
            Write-Host "  agent=$($L.agent) pid=$($L.pid) filter=$(if ($L.filter) { $L.filter } else { '(all)' }) started=$($L.startedAt)"
            if ($L.logPath) { Write-Host "  log=$($L.logPath)" }
        }
        Write-Host "editModeTest.last:"
        if ($null -eq $state.editModeTest.last) {
            Write-Host "  (none)"
        } else {
            $R = $state.editModeTest.last
            Write-Host "  agent=$($R.agent) filter=$(if ($R.filter) { $R.filter } else { '(all)' }) exitCode=$($R.exitCode) finished=$($R.finishedAt)"
            if ($R.summary) { Write-Host "  summary=$($R.summary)" }
            if ($R.resultPath) { Write-Host "  result=$($R.resultPath)" }
        }
    }
    return 0
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

$exitCode = 0
try {
    $cmd = $Command.Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($cmd) -or $cmd -eq "help" -or $cmd -eq "-h" -or $cmd -eq "--help") {
        if ($cmd -and $cmd -ne "help" -and (Test-HelpFlag $Rest)) {
            Show-Help
        } elseif ([string]::IsNullOrWhiteSpace($Command) -or $cmd -eq "help" -or $cmd -eq "-h" -or $cmd -eq "--help") {
            Show-Help
        }
        $exitCode = 0
    } else {
        switch ($cmd) {
            "status"       { $exitCode = Invoke-Status $Rest }
            "claim"        { $exitCode = Invoke-Claim $Rest }
            "release"      { $exitCode = Invoke-Release $Rest }
            "gate-restart" { $exitCode = Invoke-GateRestart $Rest }
            "test"         { $exitCode = Invoke-Test $Rest }
            "test-status"  { $exitCode = Invoke-TestStatus $Rest }
            default {
                Write-Host "Unknown command: $Command"
                Write-Host "Run with -h for help."
                $exitCode = 1
            }
        }
    }
} catch {
    $msg = $_.Exception.Message
    if ($_.InvocationInfo -and $_.InvocationInfo.PositionMessage) {
        $msg = "$msg`n$($_.InvocationInfo.PositionMessage)"
    }
    [Console]::Error.WriteLine("ai-workspace error: $msg")
    $exitCode = 1
}

exit $exitCode
