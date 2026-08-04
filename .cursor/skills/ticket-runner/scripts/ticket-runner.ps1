#Requires -Version 5.1
<#
.SYNOPSIS
  Serial GitHub Issues ticket runner via Cursor Agent CLI (fresh context per ticket).

.DESCRIPTION
  Resolves a queue, then runs one supervised headless `agent -p` per ticket with
  prompt "/implement #N". Includes hard budgets, stdout idle detection,
  one-shot AI escape-hatch intervention, and rotating local machine logs (max 3).

.NOTES
  Exit codes:
    0  success
    1  usage / config / environment error
    2  ticket run failed
    3  paused awaiting human (intervention exhausted or pause_user)
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Command = "",

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest = @()
)

$ErrorActionPreference = "Stop"
$script:SkillRoot = Split-Path -Parent $PSScriptRoot
$script:DefaultModel = "cursor-grok-4.5-high"
$script:DefaultModelNote = "Cursor CLI slug for Grok 4.5 High effort (renamed from grok-4.5-xhigh). Re-check with: agent --list-models"

# ---------------------------------------------------------------------------
# Arg helpers
# ---------------------------------------------------------------------------

function Get-FlagValue {
    param([string[]] $ArgsList, [string[]] $Names, [string] $Default = $null)
    for ($i = 0; $i -lt $ArgsList.Count; $i++) {
        $a = $ArgsList[$i]
        foreach ($n in $Names) {
            if ($a -eq $n -and ($i + 1) -lt $ArgsList.Count) { return $ArgsList[$i + 1] }
            $prefix = "$n="
            if ($a.StartsWith($prefix)) { return $a.Substring($prefix.Length) }
        }
    }
    return $Default
}

function Test-HasFlag {
    param([string[]] $ArgsList, [string[]] $Names)
    foreach ($a in $ArgsList) {
        foreach ($n in $Names) { if ($a -eq $n) { return $true } }
    }
    return $false
}

function Show-Help {
    @"
ticket-runner — serial Cursor Agent CLI landing for GitHub Issues

Usage:
  ticket-runner.ps1 <command> [options]

Commands:
  plan      Resolve and print the queue (no agent runs)
  run       Run the queue serially (supervised agent -p per ticket)
  status    Show last run state
  doctor    Check gh / Cursor agent / auth / default model
  help      This help

Queue sources (pick one; else config.defaultParent, else readyLabel):
  --parent <n>           Task-list / tracked children of parent Spec n
  --issues <a,b,c>       Explicit issue numbers (order preserved)
  --label <name>         Open issues with label (default: ready-for-agent)
  defaultParent (config) Used when no --parent/--issues/--label is passed

Run options:
  --once                 Only the first pending ticket (opt-in; default is full queue)
  --from <n>             Skip until #n (inclusive)
  --model <id>           Batch model override (default: $script:DefaultModel)
  --workspace <path>     Repo root
  --config <path>        JSON config path
  --dry-run              Alias of plan when used with run
  --strict-close         Fail if issue still open after agent exit 0
  --continue-if-open     Opposite of strict-close (default)
  --prompt-template <s>  Default: /implement #{number}
  --agent <path>         Cursor agent.cmd (not grok's agent)
  --no-force             Do not pass --force
  --json                 Machine-readable plan/status

Robustness (config defaults):
  ticketBudgetMinutes=30  hard wall-clock per ticket attempt
  stdoutIdleMinutes=10    no stdout → unhealthy → one-shot intervention
  interventionBudgetMinutes=30
  maxMachineLogRuns=3     rotating local run archives

Examples:
  ticket-runner.ps1 doctor
  ticket-runner.ps1 plan
  ticket-runner.ps1 run
  ticket-runner.ps1 run --parent 114
  ticket-runner.ps1 run --parent 114 --once
  ticket-runner.ps1 run --model cursor-grok-4.5-high
"@ | Write-Output
}

# ---------------------------------------------------------------------------
# Paths / state / machine logs
# ---------------------------------------------------------------------------

function Find-RepoRoot {
    param([string] $Start)
    $dir = if ($Start) { (Resolve-Path -LiteralPath $Start).Path } else { (Get-Location).Path }
    for ($i = 0; $i -lt 12 -and $dir; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dir ".git")) {
            return (Resolve-Path -LiteralPath $dir).Path
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

function Get-StateDir([string] $projectRoot) { Join-Path $projectRoot ".cursor\ticket-runner" }
function Get-StatePath([string] $projectRoot) { Join-Path (Get-StateDir $projectRoot) "state.json" }
function Get-RunsDir([string] $projectRoot) { Join-Path (Get-StateDir $projectRoot) "runs" }
function Get-RunsIndexPath([string] $projectRoot) { Join-Path (Get-RunsDir $projectRoot) "index.json" }

function Ensure-StateDir([string] $projectRoot) {
    foreach ($p in @((Get-StateDir $projectRoot), (Get-RunsDir $projectRoot))) {
        if (-not (Test-Path -LiteralPath $p)) {
            New-Item -ItemType Directory -Path $p -Force | Out-Null
        }
    }
}

function Get-IsoNow { (Get-Date).ToUniversalTime().ToString("o") }

function Write-JsonFileAtomic([string] $path, $obj) {
    $dir = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = $obj | ConvertTo-Json -Depth 12
    $tmp = "$path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
        Move-Item -LiteralPath $tmp -Destination $path -Force
    } catch {
        try { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force } } catch {}
        # Local disk only — swallow after one retry to avoid cascading failures.
        try {
            [System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
        } catch {
            Write-Host "[warn] Write-JsonFileAtomic failed: $path :: $_" -ForegroundColor Yellow
        }
    }
}

function Write-JsonFile([string] $path, $obj) { Write-JsonFileAtomic $path $obj }

function Read-JsonFile([string] $path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return $raw | ConvertFrom-Json
}

function Append-Ndjson([string] $path, $obj) {
    try {
        $dir = Split-Path -Parent $path
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        $line = ($obj | ConvertTo-Json -Compress -Depth 8)
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::AppendAllText($path, $line + [Environment]::NewLine, $utf8)
    } catch {
        Write-Host "[warn] Append-Ndjson failed: $path :: $_" -ForegroundColor Yellow
    }
}

function Write-MachineEvent {
    param([string] $EventsPath, [string] $Type, [hashtable] $Fields = @{})
    $payload = [ordered]@{ ts = Get-IsoNow; type = $Type }
    foreach ($k in $Fields.Keys) { $payload[$k] = $Fields[$k] }
    Append-Ndjson -path $EventsPath -obj $payload
}

function Get-DefaultConfig {
    return [ordered]@{
        model                       = $script:DefaultModel
        promptTemplate              = "/implement #{number}"
        agentPath                   = ""
        strictClose                 = $false
        readyLabel                  = "ready-for-agent"
        defaultParent               = $null
        force                       = $true
        trust                       = $true
        outputFormat                = "stream-json"
        ticketBudgetMinutes         = 30
        stdoutIdleMinutes           = 10
        interventionBudgetMinutes   = 30
        interventionGraceMinutes    = 15
        pollSeconds                 = 5
        maxMachineLogRuns           = 3
        hooks                       = [ordered]@{
            beforeTicket = ""
            afterTicket  = ""
        }
    }
}

function Merge-Config($base, $overlay) {
    if (-not $overlay) { return $base }
    $out = [ordered]@{}
    foreach ($k in $base.Keys) { $out[$k] = $base[$k] }
    foreach ($prop in $overlay.PSObject.Properties) {
        $name = $prop.Name
        if ($name -eq "hooks" -and $prop.Value) {
            $hooks = [ordered]@{}
            if ($out.hooks) { foreach ($hk in $out.hooks.Keys) { $hooks[$hk] = $out.hooks[$hk] } }
            foreach ($hp in $prop.Value.PSObject.Properties) { $hooks[$hp.Name] = [string]$hp.Value }
            $out.hooks = $hooks
        } else {
            $out[$name] = $prop.Value
        }
    }
    return $out
}

function Load-Config {
    param([string] $RepoRoot, [string] $ExplicitPath)
    $cfg = Get-DefaultConfig
    $example = Join-Path $script:SkillRoot "config.example.json"
    if (Test-Path -LiteralPath $example) { $cfg = Merge-Config $cfg (Read-JsonFile $example) }
    $projectCfg = Join-Path $RepoRoot ".cursor\ticket-runner.config.json"
    if (Test-Path -LiteralPath $projectCfg) { $cfg = Merge-Config $cfg (Read-JsonFile $projectCfg) }
    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) { throw "Config not found: $ExplicitPath" }
        $cfg = Merge-Config $cfg (Read-JsonFile $ExplicitPath)
    }
    if ([string]::IsNullOrWhiteSpace([string]$cfg.model)) {
        $cfg.model = $script:DefaultModel
    }
    if ([string]::IsNullOrWhiteSpace([string]$cfg.outputFormat)) {
        $cfg.outputFormat = "stream-json"
    }
    return $cfg
}

function New-MachineRun {
    param([string] $RepoRoot, [int] $MaxRuns, [string] $Model, $QueueNumbers)
    Ensure-StateDir $RepoRoot
    $runsDir = Get-RunsDir $RepoRoot
    $indexPath = Get-RunsIndexPath $RepoRoot
    $index = Read-JsonFile $indexPath
    if (-not $index) { $index = [pscustomobject]@{ max = $MaxRuns; runs = @() } }
    $runs = @()
    if ($index.runs) { $runs = @($index.runs) }

    while ($runs.Count -ge $MaxRuns) {
        $oldest = $runs[0]
        $oldPath = [string]$oldest.path
        if (-not $oldPath) { $oldPath = Join-Path $runsDir ([string]$oldest.id) }
        if (Test-Path -LiteralPath $oldPath) {
            try { Remove-Item -LiteralPath $oldPath -Recurse -Force -ErrorAction Stop } catch {
                Write-Host "[warn] could not remove old run log: $oldPath :: $_" -ForegroundColor Yellow
            }
        }
        if ($runs.Count -eq 1) { $runs = @() } else { $runs = @($runs[1..($runs.Count - 1)]) }
    }

    $runId = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss") + "-" + ([guid]::NewGuid().ToString("N").Substring(0, 8))
    $runDir = Join-Path $runsDir $runId
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $runDir "tickets") -Force | Out-Null

    $meta = [ordered]@{
        id        = $runId
        startedAt = Get-IsoNow
        model     = $Model
        queue     = @($QueueNumbers)
        path      = $runDir
        status    = "running"
    }
    Write-JsonFileAtomic (Join-Path $runDir "run.json") $meta

    $runs += ,[pscustomobject]@{ id = $runId; startedAt = $meta.startedAt; path = $runDir; model = $Model }
    $newIndex = [ordered]@{ max = $MaxRuns; updatedAt = Get-IsoNow; runs = $runs }
    Write-JsonFileAtomic $indexPath $newIndex
    return $runDir
}

# ---------------------------------------------------------------------------
# Cursor agent resolution
# ---------------------------------------------------------------------------

function Resolve-CursorAgentNodeLaunch {
    # Prefer node.exe + index.js so Start-Process ArgumentList keeps spaces
    # (agent.cmd → cmd.exe re-parses and splits paths like "Ninegrid Gambit").
    param([string] $AgentCmdOrDir)
    $root = $AgentCmdOrDir
    if (Test-Path -LiteralPath $root -PathType Leaf) {
        $root = Split-Path -Parent $root
    }
    if (-not $root -or -not (Test-Path -LiteralPath $root)) { return $null }

    $localNode = Join-Path $root "node.exe"
    $localIndex = Join-Path $root "index.js"
    if ((Test-Path -LiteralPath $localNode) -and (Test-Path -LiteralPath $localIndex)) {
        return [pscustomobject]@{
            FilePath   = (Resolve-Path -LiteralPath $localNode).Path
            PrefixArgs = @((Resolve-Path -LiteralPath $localIndex).Path)
        }
    }

    $versionsRoot = Join-Path $root "versions"
    if (-not (Test-Path -LiteralPath $versionsRoot)) { return $null }
    $versionDir = Get-ChildItem -LiteralPath $versionsRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{4}\.\d{1,2}\.\d{1,2}(-\d{2}-\d{2}-\d{2})?-[a-f0-9]+$' } |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if (-not $versionDir) { return $null }
    $nodePath = Join-Path $versionDir.FullName "node.exe"
    $indexPath = Join-Path $versionDir.FullName "index.js"
    if ((Test-Path -LiteralPath $nodePath) -and (Test-Path -LiteralPath $indexPath)) {
        return [pscustomobject]@{
            FilePath   = (Resolve-Path -LiteralPath $nodePath).Path
            PrefixArgs = @((Resolve-Path -LiteralPath $indexPath).Path)
        }
    }
    return $null
}

function Resolve-CursorAgent {
    param([string] $Override)
    if ($Override -and (Test-Path -LiteralPath $Override)) {
        return (Resolve-Path -LiteralPath $Override).Path
    }
    if ($env:CURSOR_AGENT -and (Test-Path -LiteralPath $env:CURSOR_AGENT)) {
        return (Resolve-Path -LiteralPath $env:CURSOR_AGENT).Path
    }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "cursor-agent\agent.cmd"),
        (Join-Path $env:LOCALAPPDATA "cursor-agent\agent.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return (Resolve-Path -LiteralPath $c).Path }
    }
    $cmd = Get-Command agent -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and ($cmd.Source -notmatch '[\\/]\.grok[\\/]') -and ($cmd.Source -notmatch '[\\/]grok[\\/]bin[\\/]')) {
        return $cmd.Source
    }
    throw @"
Cursor Agent CLI not found.
Install/login: https://cursor.com/docs/cli/headless
Or set CURSOR_AGENT to agent.cmd (%LOCALAPPDATA%\cursor-agent\agent.cmd)
Note: PATH may resolve to grok's agent.exe — this runner refuses that binary.
"@
}

function Test-AgentAuth {
    param([string] $AgentPath)
    if ($env:CURSOR_API_KEY -or $env:CURSOR_AUTH_TOKEN) { return $true }
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $AgentPath
        $psi.Arguments = "status"
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
        $p = [System.Diagnostics.Process]::Start($psi)
        $out = $p.StandardOutput.ReadToEnd() + $p.StandardError.ReadToEnd()
        $p.WaitForExit(15000) | Out-Null
        if ($out -match '(?i)not logged in|authentication required|please run .+login') { return $false }
        if ($out -match '(?i)logged in|authenticated|email\s*:') { return $true }
        return $false
    } catch { return $false }
}

function Get-AgentModelIds {
    param([string] $AgentPath)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $AgentPath
    $psi.Arguments = "--list-models"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $p = [System.Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd() + $p.StandardError.ReadToEnd()
    $p.WaitForExit(60000) | Out-Null
    if ($p.ExitCode -ne 0) {
        return [pscustomobject]@{ ok = $false; raw = $out; ids = @() }
    }
    $ids = @()
    foreach ($line in ($out -split "`r?`n")) {
        if ($line -match '^\s*([A-Za-z0-9._\-]+)\s') {
            $ids += $Matches[1]
        } elseif ($line -match '^\s*([A-Za-z0-9._\-]+)\s*$') {
            $ids += $Matches[1]
        }
    }
    return [pscustomobject]@{ ok = $true; raw = $out; ids = $ids }
}

function Test-ModelIdFormat([string] $Model) {
    # Cursor slugs: letters/digits/dot/hyphen, optional [param=value,...]
    return ($Model -match '^[A-Za-z0-9][A-Za-z0-9._\-]*(?:\[[^\]]+\])?$')
}

# ---------------------------------------------------------------------------
# GitHub
# ---------------------------------------------------------------------------

function Invoke-GhJson {
    param([string[]] $GhArgs)
    $ghCmd = Get-Command gh -ErrorAction Stop
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ghCmd.Source
    $quoted = @()
    foreach ($a in $GhArgs) {
        if ($null -eq $a) { continue }
        $s = [string]$a
        if ($s -match '[\s"]') { $quoted += '"' + ($s.Replace('"', '\"')) + '"' } else { $quoted += $s }
    }
    $psi.Arguments = ($quoted -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    [void]$proc.Start()
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) { throw "gh failed ($($proc.ExitCode)): $stderr$stdout" }
    $text = $stdout.Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

function Get-RepoSlug { Invoke-GhJson @("repo", "view", "--json", "nameWithOwner,owner,name") }

function Get-TrackedIssueNumbers {
    param([int] $Parent)
    $repo = Get-RepoSlug
    $owner = $repo.owner.login
    $name = $repo.name
    $query = @'
query($o:String!,$n:String!,$num:Int!){
  repository(owner:$o,name:$n){
    issue(number:$num){ trackedIssues(first:50){ nodes { number state } } }
  }
}
'@
    try {
        $obj = Invoke-GhJson @("api", "graphql", "-f", "query=$query", "-F", "o=$owner", "-F", "n=$name", "-F", "num=$Parent")
        $nodes = $obj.data.repository.issue.trackedIssues.nodes
        if (-not $nodes) { return @() }
        return @($nodes | ForEach-Object { [int]$_.number })
    } catch { return @() }
}

function Get-TaskListIssueNumbers {
    param([string] $Body)
    if ([string]::IsNullOrWhiteSpace($Body)) { return @() }
    $nums = New-Object System.Collections.Generic.List[int]
    $seen = @{}
    $rx = [regex]'(?m)^\s*-\s*\[(?: |x|X)\]\s*#(\d+)\b'
    foreach ($m in $rx.Matches($Body)) {
        $n = [int]$m.Groups[1].Value
        if (-not $seen.ContainsKey($n)) { $seen[$n] = $true; $nums.Add($n) | Out-Null }
    }
    return @($nums)
}

function Get-IssueMeta {
    param([int] $Number)
    Invoke-GhJson @("issue", "view", "$Number", "--json", "number,title,state,labels")
}

function Resolve-Queue {
    param(
        [Nullable[int]] $Parent,
        [int[]] $Issues,
        [string] $Label,
        [Nullable[int]] $From,
        [bool] $IncludeClosed = $false
    )
    $ordered = New-Object System.Collections.Generic.List[int]
    if ($Issues -and $Issues.Count -gt 0) {
        foreach ($n in $Issues) { $ordered.Add([int]$n) | Out-Null }
    }
    elseif ($Parent) {
        $bodyObj = Invoke-GhJson @("issue", "view", "$Parent", "--json", "body")
        $fromTask = Get-TaskListIssueNumbers -Body $bodyObj.body
        $fromTracked = Get-TrackedIssueNumbers -Parent $Parent
        if ($fromTask.Count -gt 0) { foreach ($n in $fromTask) { $ordered.Add($n) | Out-Null } }
        elseif ($fromTracked.Count -gt 0) { foreach ($n in $fromTracked) { $ordered.Add($n) | Out-Null } }
        else { throw "Parent #$Parent has no task-list children (#N) and no trackedIssues." }
    }
    elseif ($Label) {
        $list = Invoke-GhJson @("issue", "list", "--state", "open", "--label", $Label, "--limit", "100", "--json", "number,title")
        foreach ($it in ($list | Sort-Object number)) { $ordered.Add([int]$it.number) | Out-Null }
    }
    else { throw "Provide --parent, --issues, or --label." }

    if ($From) {
        $idx = 0; $found = $false
        for ($i = 0; $i -lt $ordered.Count; $i++) {
            if ($ordered[$i] -eq $From) { $idx = $i; $found = $true; break }
        }
        if (-not $found) { throw "--from #$From not in queue." }
        $ordered = [System.Collections.Generic.List[int]]@($ordered.GetRange($idx, $ordered.Count - $idx))
    }

    $items = @()
    foreach ($n in $ordered) {
        $meta = Get-IssueMeta -Number $n
        $labels = @()
        if ($meta.labels) { $labels = @($meta.labels | ForEach-Object { $_.name }) }
        $state = [string]$meta.state
        if (-not $IncludeClosed -and $state -eq "CLOSED") { continue }
        $items += [pscustomobject]@{
            number = [int]$meta.number
            title  = [string]$meta.title
            state  = $state
            labels = $labels
        }
    }
    return $items
}

function Format-Prompt {
    param([string] $Template, [int] $Number, [string] $Title)
    $p = $Template
    $p = $p.Replace("#{number}", "#$Number")
    $p = $p.Replace("{number}", "$Number")
    $p = $p.Replace("{title}", $Title)
    return $p
}

function Build-HeadlessPrompt {
    param([string] $UserPrompt)
    @"
[ticket-runner headless]
You are running non-interactively. Do NOT ask the user questions. Do NOT wait for confirmation.
If information is missing, choose the safest reasonable default and proceed.
Do not use git worktrees. Do not start other tickets in this session.

User task:
$UserPrompt
"@
}

# ---------------------------------------------------------------------------
# Hooks
# ---------------------------------------------------------------------------

function Invoke-Hook {
    param([string] $CommandLine, [string] $RepoRoot, [int] $Number, [string] $Phase)
    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return }
    Write-Host "[hook:$Phase] $CommandLine" -ForegroundColor DarkCyan
    $env:TICKET_RUNNER_ISSUE = "$Number"
    $env:TICKET_RUNNER_PHASE = $Phase
    $env:TICKET_RUNNER_ROOT = $RepoRoot
    Push-Location $RepoRoot
    try {
        cmd.exe /c $CommandLine
        if ($LASTEXITCODE -ne 0) { throw "Hook failed ($Phase) exit=$LASTEXITCODE" }
    } finally {
        Pop-Location
        Remove-Item Env:TICKET_RUNNER_ISSUE -ErrorAction SilentlyContinue
        Remove-Item Env:TICKET_RUNNER_PHASE -ErrorAction SilentlyContinue
        Remove-Item Env:TICKET_RUNNER_ROOT -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Supervised agent process (no stdout inherit / hang)
# ---------------------------------------------------------------------------

function Stop-ProcessTree([int] $ProcessId) {
    if ($ProcessId -le 0) { return }
    try {
        cmd.exe /c "taskkill /PID $ProcessId /T /F" | Out-Null
    } catch {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Escape-WinProcessArgument {
    # Windows CreateProcess command-line rules (PS 5.1 joins -ArgumentList arrays
    # with bare spaces, so we must emit one pre-quoted argument string).
    param([string] $Value)
    if ($null -eq $Value) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    $escaped = (($Value -replace '(\\*)"','$1$1\"') -replace '(\\+)$','$1$1')
    return '"' + $escaped + '"'
}

function Join-WinProcessArguments {
    param([string[]] $Arguments)
    return (($Arguments | ForEach-Object { Escape-WinProcessArgument $_ }) -join ' ')
}

function Quote-CmdArgument {
    # Fallback when we must still launch via .cmd (cmd.exe re-tokenize).
    param([string] $Value)
    if ($null -eq $Value) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    $escaped = $Value.Replace('"', '""')
    return "`"$escaped`""
}

function Start-RedirectedAgent {
    param(
        [string] $AgentPath,
        [string] $RepoRoot,
        [string] $Prompt,
        [string] $Model,
        [bool] $Force,
        [bool] $Trust,
        [string] $OutputFormat,
        [string] $StdoutPath,
        [string] $StderrPath
    )

    $filePath = $AgentPath
    $argList = New-Object System.Collections.Generic.List[string]

    # Prefer node.exe + index.js (avoids agent.cmd → cmd.exe).
    $nodeLaunch = Resolve-CursorAgentNodeLaunch -AgentCmdOrDir $AgentPath
    if ($nodeLaunch) {
        $filePath = [string]$nodeLaunch.FilePath
        foreach ($pre in @($nodeLaunch.PrefixArgs)) { $argList.Add([string]$pre) | Out-Null }
    }

    $argList.Add("-p") | Out-Null
    $argList.Add("--workspace") | Out-Null
    $argList.Add($RepoRoot) | Out-Null
    $argList.Add("--output-format") | Out-Null
    $argList.Add($OutputFormat) | Out-Null
    if ($Force) { $argList.Add("--force") | Out-Null }
    if ($Trust) { $argList.Add("--trust") | Out-Null }
    if ($Model) {
        $argList.Add("--model") | Out-Null
        $argList.Add($Model) | Out-Null
    }
    $argList.Add($Prompt) | Out-Null

    # PS 5.1: pass ONE string; array form silently drops required quoting.
    if ($filePath -match '\.(cmd|bat)$') {
        $argString = (($argList | ForEach-Object { Quote-CmdArgument $_ }) -join ' ')
    } else {
        $argString = Join-WinProcessArguments -Arguments $argList.ToArray()
    }

    # Ensure empty log files (Start-Process refuses to overwrite sometimes inconsistently)
    [System.IO.File]::WriteAllText($StdoutPath, "", [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($StderrPath, "", [System.Text.UTF8Encoding]::new($false))

    # File redirects: child does NOT inherit parent console pipes → avoids parent hang.
    $proc = Start-Process -FilePath $filePath `
        -ArgumentList $argString `
        -WorkingDirectory $RepoRoot `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput $StdoutPath `
        -RedirectStandardError $StderrPath

    $sync = @{
        lastOutputUtc = [DateTime]::UtcNow
        lastSize      = 0L
        parseErrors   = 0
        authSuspected = $false
        askSuspected  = $false
        lineCount     = 0
        outputFormat  = $OutputFormat
        stdoutPath    = $StdoutPath
        stderrPath    = $StderrPath
        scanOffset    = 0L
        stderrScanOffset = 0L
        streamResultSuccess = $null   # $true / $false / $null when unseen
        streamResultError   = $false
        softAuthLogged      = $false
        softAskLogged       = $false
    }

    return [pscustomobject]@{
        Process   = $proc
        Sync      = $sync
        Arguments = $argString
        FilePath  = $filePath
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }
}

function Test-CursorAgentAuthText {
    # Real Cursor Agent CLI / account failures only — NOT Unity Pipeline 401,
    # NOT model thinking that quotes "401 Unauthorized", NOT tool stdout noise.
    param([string] $Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    if ($Text -match '(?i)unity.*(401|unauthorized)|pipeline.*(401|unauthorized)|HTTP\s*401') {
        return $false
    }
    return [bool]($Text -match '(?i)authentication required|not logged in to cursor|please run .*(agent|cursor).*login|invalid (api[_ ]?key|cursor.*token)|CURSOR_API_KEY.*(invalid|missing|expired)|unauthorized.*cursor agent|agent login required')
}

function Update-AgentLogSignals($Sync) {
    try {
        $paths = @([string]$Sync.stdoutPath, [string]$Sync.stderrPath)
        $totalSize = 0L
        foreach ($p in $paths) {
            if (Test-Path -LiteralPath $p) {
                $totalSize += (Get-Item -LiteralPath $p).Length
            }
        }
        if ($totalSize -gt [int64]$Sync.lastSize) {
            $Sync.lastSize = $totalSize
            $Sync.lastOutputUtc = [DateTime]::UtcNow
        }

        $chunks = @()
        foreach ($pathKey in @("stdoutPath", "stderrPath")) {
            $path = [string]$Sync[$pathKey]
            if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) { continue }
            $offsetKey = if ($pathKey -eq "stdoutPath") { "scanOffset" } else { "stderrScanOffset" }
            if ($null -eq $Sync[$offsetKey]) { $Sync[$offsetKey] = 0L }
            $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                if ($fs.Length -lt [int64]$Sync[$offsetKey]) { $Sync[$offsetKey] = 0L }
                if ($fs.Length -eq [int64]$Sync[$offsetKey]) { continue }
                $fs.Seek([int64]$Sync[$offsetKey], [System.IO.SeekOrigin]::Begin) | Out-Null
                $reader = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8, $true, 4096, $true)
                $piece = $reader.ReadToEnd()
                $Sync[$offsetKey] = $fs.Position
                $reader.Dispose()
                if (-not [string]::IsNullOrEmpty($piece)) {
                    $chunks += [pscustomobject]@{ pathKey = $pathKey; text = $piece }
                }
            } finally { $fs.Dispose() }
        }

        foreach ($chunkInfo in $chunks) {
            $isStderr = ($chunkInfo.pathKey -eq "stderrPath")
            foreach ($line in ($chunkInfo.text -split "`r?`n")) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $Sync.lineCount = [int]$Sync.lineCount + 1

                if ($isStderr) {
                    # Agent CLI auth errors usually land on stderr as plain text.
                    if (Test-CursorAgentAuthText $line) { $Sync.authSuspected = $true }
                    continue
                }

                if ([string]$Sync.outputFormat -eq "stream-json") {
                    $t = $line.Trim()
                    if ($t.StartsWith("{") -and $t.EndsWith("}")) {
                        try {
                            $obj = $t | ConvertFrom-Json
                            $otype = [string]$obj.type

                            if ($otype -eq "result") {
                                $isErr = $false
                                if ($null -ne $obj.is_error) { $isErr = [bool]$obj.is_error }
                                $Sync.streamResultError = $isErr
                                $Sync.streamResultSuccess = (-not $isErr -and [string]$obj.subtype -eq "success")
                                $resText = ""
                                if ($obj.result) { $resText = [string]$obj.result }
                                if ($isErr -and (Test-CursorAgentAuthText $resText)) { $Sync.authSuspected = $true }
                            }
                            elseif ($otype -eq "system") {
                                $sysText = ""
                                if ($obj.message) { $sysText = [string]$obj.message }
                                elseif ($obj.text) { $sysText = [string]$obj.text }
                                if (Test-CursorAgentAuthText $sysText) { $Sync.authSuspected = $true }
                            }
                            elseif ($otype -eq "assistant") {
                                $parts = @()
                                if ($obj.message -and $obj.message.content) {
                                    foreach ($c in @($obj.message.content)) {
                                        if ($c.type -eq "text" -and $c.text) { $parts += [string]$c.text }
                                    }
                                }
                                if ($parts.Count -gt 0) {
                                    $askProbe = ($parts -join "`n")
                                    if ($askProbe -match '(?i)\b(which (option|approach) should I|do you want me to|please (confirm|choose|answer)|waiting for (your|user) (input|reply)|I need you to (confirm|choose|clarify))\b') {
                                        $Sync.askSuspected = $true
                                    }
                                }
                            }
                            # intentionally ignore: thinking, tool_call payloads (Unity 401 lives there)
                        } catch {
                            $Sync.parseErrors = [int]$Sync.parseErrors + 1
                        }
                    }
                } else {
                    # text mode: still avoid bare "unauthorized" / HTTP 401
                    if (Test-CursorAgentAuthText $line) { $Sync.authSuspected = $true }
                    if ($line -match '(?i)\b(which (option|approach) should I|do you want me to|please (confirm|choose|answer)|waiting for (your|user) (input|reply)|I need you to (confirm|choose|clarify))\b') {
                        $Sync.askSuspected = $true
                    }
                }
            }
        }
    } catch {
        # log scan must never kill the supervisor
    }
}

function Close-RedirectedAgent($Handle) {
    if (-not $Handle) { return }
    try {
        if ($Handle.Process -and -not $Handle.Process.HasExited) {
            Stop-ProcessTree -ProcessId $Handle.Process.Id
            try { $Handle.Process.WaitForExit(5000) | Out-Null } catch {}
        }
    } catch {}
    try { $Handle.Process.Dispose() } catch {}
}

function Invoke-EscapeHatchIntervention {
    param(
        [string] $AgentPath,
        [string] $RepoRoot,
        [string] $Model,
        [bool] $Force,
        [bool] $Trust,
        [string] $TicketDir,
        [int] $IssueNumber,
        [int] $WorkerPid,
        [string] $Reason,
        [int] $BudgetMinutes,
        [string] $EventsPath
    )

    $decisionPath = Join-Path $TicketDir "intervention-decision.json"
    $intervDir = Join-Path $TicketDir "intervention"
    if (-not (Test-Path -LiteralPath $intervDir)) {
        New-Item -ItemType Directory -Path $intervDir -Force | Out-Null
    }
    $stdoutPath = Join-Path $intervDir "stdout.log"
    $stderrPath = Join-Path $intervDir "stderr.log"

    $prompt = @"
[ticket-runner escape-hatch — ONE SHOT, NON-RECURSIVE]
A supervised ticket worker looks unhealthy.

Issue: #$IssueNumber
Worker PID: $WorkerPid
Unhealthy reason: $Reason
Inspect local files under: $TicketDir
(stdout.log / stderr.log / events.ndjson / meta.json)

Write EXACTLY one JSON file to this path (overwrite ok):
$decisionPath

Schema:
{"action":"kill_restart"|"continue_wait"|"pause_user","reason":"short string"}

Rules:
- Do NOT implement the ticket.
- Do NOT ask questions.
- Prefer kill_restart if the process is wedged / auth dead / asking for input.
- Prefer continue_wait only if logs show real progress and a false-positive timeout is likely.
- Prefer pause_user if human judgment is required or state is ambiguous after kill would lose work.
"@

    Write-MachineEvent $EventsPath "intervention_start" @{ reason = $Reason; pid = $WorkerPid; budgetMin = $BudgetMinutes }
    Write-Host "[intervention] spawning one-shot escape-hatch agent (budget ${BudgetMinutes}m)" -ForegroundColor Magenta

    $handle = Start-RedirectedAgent `
        -AgentPath $AgentPath `
        -RepoRoot $RepoRoot `
        -Prompt $prompt `
        -Model $Model `
        -Force $Force `
        -Trust $Trust `
        -OutputFormat "text" `
        -StdoutPath $stdoutPath `
        -StderrPath $stderrPath

    $deadline = [DateTime]::UtcNow.AddMinutes($BudgetMinutes)
    $action = $null
    try {
        while (-not $handle.Process.HasExited) {
            if ([DateTime]::UtcNow -gt $deadline) {
                Write-MachineEvent $EventsPath "intervention_timeout" @{ pid = $handle.Process.Id }
                Stop-ProcessTree -ProcessId $handle.Process.Id
                break
            }
            Start-Sleep -Seconds 5
        }
        try { $handle.Process.WaitForExit(3000) | Out-Null } catch {}
    } finally {
        Close-RedirectedAgent $handle
    }

    if (Test-Path -LiteralPath $decisionPath) {
        try {
            $dec = Read-JsonFile $decisionPath
            $action = [string]$dec.action
            Write-MachineEvent $EventsPath "intervention_decision" @{ action = $action; reason = [string]$dec.reason }
        } catch {
            Write-MachineEvent $EventsPath "intervention_decision_parse_error" @{ error = "$_" }
        }
    } else {
        Write-MachineEvent $EventsPath "intervention_no_decision_file" @{}
    }

    if ($action -notin @("kill_restart", "continue_wait", "pause_user")) {
        # Unusable intervention → human
        return "pause_user"
    }
    return $action
}

function Invoke-SupervisedTicket {
    param(
        [string] $AgentPath,
        [string] $RepoRoot,
        $Config,
        [string] $Model,
        [int] $IssueNumber,
        [string] $Title,
        [string] $UserPrompt,
        [string] $TicketDir
    )

    $eventsPath = Join-Path $TicketDir "events.ndjson"
    $stdoutPath = Join-Path $TicketDir "stdout.log"
    $stderrPath = Join-Path $TicketDir "stderr.log"
    $metaPath = Join-Path $TicketDir "meta.json"

    $budgetMin = [int]$Config.ticketBudgetMinutes
    if ($budgetMin -le 0) { $budgetMin = 30 }
    $idleMin = [int]$Config.stdoutIdleMinutes
    if ($idleMin -le 0) { $idleMin = 10 }
    $intervBudget = [int]$Config.interventionBudgetMinutes
    if ($intervBudget -le 0) { $intervBudget = 30 }
    $graceMin = [int]$Config.interventionGraceMinutes
    if ($graceMin -le 0) { $graceMin = 15 }
    $poll = [int]$Config.pollSeconds
    if ($poll -le 0) { $poll = 5 }

    $fullPrompt = Build-HeadlessPrompt -UserPrompt $UserPrompt
    $attempt = 0
    $intervened = $false
    $maxAttempts = 2  # original + one kill_restart

    while ($attempt -lt $maxAttempts) {
        $attempt++
        Write-MachineEvent $eventsPath "attempt_start" @{ attempt = $attempt; model = $Model; issue = $IssueNumber }
        Write-JsonFileAtomic $metaPath ([ordered]@{
            issue     = $IssueNumber
            title     = $Title
            model     = $Model
            attempt   = $attempt
            startedAt = Get-IsoNow
            status    = "running"
        })

        $handle = Start-RedirectedAgent `
            -AgentPath $AgentPath `
            -RepoRoot $RepoRoot `
            -Prompt $fullPrompt `
            -Model $Model `
            -Force ([bool]$Config.force) `
            -Trust ([bool]$Config.trust) `
            -OutputFormat ([string]$Config.outputFormat) `
            -StdoutPath $stdoutPath `
            -StderrPath $stderrPath

        Write-Host "[agent] pid=$($handle.Process.Id) model=$Model issue=#$IssueNumber attempt=$attempt" -ForegroundColor Cyan
        Write-MachineEvent $eventsPath "process_started" @{ pid = $handle.Process.Id; args = $handle.Arguments }

        $startedUtc = [DateTime]::UtcNow
        $deadline = $startedUtc.AddMinutes($budgetMin)
        $continueGraceDeadline = $null
        $result = $null

        try {
            while (-not $handle.Process.HasExited) {
                Start-Sleep -Seconds $poll
                try { $handle.Process.Refresh() } catch {}
                Update-AgentLogSignals -Sync $handle.Sync
                $now = [DateTime]::UtcNow
                $idleSec = ($now - [DateTime]$handle.Sync.lastOutputUtc).TotalSeconds
                $unhealthyReason = $null

                # Soft signals only: log once, do NOT open escape-hatch.
                # Normal Unity 401 / thinking noise must never burn an intervention.
                if ($handle.Sync.authSuspected -and -not $handle.Sync.softAuthLogged) {
                    $handle.Sync.softAuthLogged = $true
                    Write-MachineEvent $eventsPath "soft_signal" @{ kind = "auth_or_token"; note = "observed only; no intervention" }
                    Write-Host "[soft] auth_or_token observed (no intervention)" -ForegroundColor DarkGray
                }
                if ($handle.Sync.askSuspected -and -not $handle.Sync.softAskLogged) {
                    $handle.Sync.softAskLogged = $true
                    Write-MachineEvent $eventsPath "soft_signal" @{ kind = "agent_asking_questions"; note = "observed only; no intervention" }
                    Write-Host "[soft] agent_asking_questions observed (no intervention)" -ForegroundColor DarkGray
                }

                # Hard triggers only — expected rare on a healthy ticket.
                if ([int]$handle.Sync.parseErrors -ge 8) { $unhealthyReason = "stream_json_parse_errors" }
                elseif ($idleSec -ge ($idleMin * 60)) { $unhealthyReason = "stdout_idle" }
                elseif ($now -gt $deadline) { $unhealthyReason = "ticket_budget_exceeded" }
                elseif ($continueGraceDeadline -and $now -gt $continueGraceDeadline) {
                    $unhealthyReason = "continue_wait_grace_exceeded"
                }

                if ($unhealthyReason) {
                    # Worker already done → never block the queue on escape-hatch.
                    try { $handle.Process.Refresh() } catch {}
                    if ($handle.Process.HasExited -or $handle.Sync.streamResultSuccess -eq $true) {
                        Write-MachineEvent $eventsPath "unhealthy_ignored_process_done" @{
                            reason = $unhealthyReason
                            hasExited = [bool]$handle.Process.HasExited
                            streamOk = $handle.Sync.streamResultSuccess
                        }
                        Write-Host "[unhealthy] $unhealthyReason ignored — worker already finished" -ForegroundColor DarkYellow
                        break
                    }

                    Write-MachineEvent $eventsPath "unhealthy" @{
                        reason   = $unhealthyReason
                        pid      = $handle.Process.Id
                        idleSec  = [int]$idleSec
                        parseErr = [int]$handle.Sync.parseErrors
                    }
                    Write-Host "[unhealthy] $unhealthyReason (pid=$($handle.Process.Id))" -ForegroundColor Yellow

                    if ($unhealthyReason -eq "continue_wait_grace_exceeded") {
                        Stop-ProcessTree -ProcessId $handle.Process.Id
                        $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = $unhealthyReason }
                        break
                    }

                    if (-not $intervened) {
                        $intervened = $true
                        $action = Invoke-EscapeHatchIntervention `
                            -AgentPath $AgentPath `
                            -RepoRoot $RepoRoot `
                            -Model $Model `
                            -Force ([bool]$Config.force) `
                            -Trust ([bool]$Config.trust) `
                            -TicketDir $TicketDir `
                            -IssueNumber $IssueNumber `
                            -WorkerPid $handle.Process.Id `
                            -Reason $unhealthyReason `
                            -BudgetMinutes $intervBudget `
                            -EventsPath $eventsPath

                        if ($action -eq "continue_wait") {
                            Write-Host "[intervention] continue_wait — grace ${graceMin}m" -ForegroundColor Magenta
                            $continueGraceDeadline = [DateTime]::UtcNow.AddMinutes($graceMin)
                            # reset idle / ask / auth so we don't immediately re-fire the same signal
                            $handle.Sync.lastOutputUtc = [DateTime]::UtcNow
                            $handle.Sync.askSuspected = $false
                            $handle.Sync.authSuspected = $false
                            continue
                        }
                        elseif ($action -eq "kill_restart") {
                            Write-Host "[intervention] kill_restart" -ForegroundColor Magenta
                            Stop-ProcessTree -ProcessId $handle.Process.Id
                            $handle.Process.WaitForExit(8000) | Out-Null
                            Close-RedirectedAgent $handle
                            $handle = $null
                            # Archive current logs before restart
                            $bak = Join-Path $TicketDir ("attempt-{0}" -f $attempt)
                            if (-not (Test-Path -LiteralPath $bak)) { New-Item -ItemType Directory -Path $bak -Force | Out-Null }
                            foreach ($f in @("stdout.log", "stderr.log")) {
                                $src = Join-Path $TicketDir $f
                                if (Test-Path -LiteralPath $src) {
                                    Copy-Item $src (Join-Path $bak $f) -Force -ErrorAction SilentlyContinue
                                }
                            }
                            $result = [pscustomobject]@{ status = "restart"; exitCode = -1; reason = $unhealthyReason }
                            break
                        }
                        else {
                            # pause_user or unknown
                            Stop-ProcessTree -ProcessId $handle.Process.Id
                            $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = $unhealthyReason }
                            break
                        }
                    } else {
                        # Already intervened once — no recursion
                        Write-MachineEvent $eventsPath "intervention_exhausted" @{ reason = $unhealthyReason }
                        Stop-ProcessTree -ProcessId $handle.Process.Id
                        $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = "intervention_exhausted:$unhealthyReason" }
                        break
                    }
                }
            }

            if ($null -eq $result) {
                try { $handle.Process.Refresh() } catch {}
                try { $handle.Process.WaitForExit(3000) | Out-Null } catch {}
                Update-AgentLogSignals -Sync $handle.Sync
                $code = $null
                try { $code = $handle.Process.ExitCode } catch {}
                # PS/Start-Process sometimes yields $null ExitCode even after a clean node exit;
                # trust stream-json {"type":"result","subtype":"success"} when present.
                if ($null -eq $code) {
                    if ($handle.Sync.streamResultSuccess -eq $true) { $code = 0 }
                    elseif ($handle.Sync.streamResultError -eq $true) { $code = 1 }
                    else { $code = 1 }
                }
                if ($handle.Sync.authSuspected -and $code -ne 0) {
                    $result = [pscustomobject]@{ status = "failed"; exitCode = $code; reason = "auth_or_token" }
                } else {
                    $result = [pscustomobject]@{
                        status   = $(if ($code -eq 0) { "finished" } else { "failed" })
                        exitCode = $code
                        reason   = $(if ($code -eq 0 -and $null -eq $handle.Process.ExitCode -and $handle.Sync.streamResultSuccess) {
                            "process_exit_stream_ok"
                        } else {
                            "process_exit"
                        })
                    }
                }
            }
        } finally {
            if ($handle) { Close-RedirectedAgent $handle }
        }

        Write-JsonFileAtomic $metaPath ([ordered]@{
            issue      = $IssueNumber
            title      = $Title
            model      = $Model
            attempt    = $attempt
            finishedAt = Get-IsoNow
            status     = $result.status
            exitCode   = $result.exitCode
            reason     = $result.reason
        })
        Write-MachineEvent $eventsPath "attempt_end" @{
            status = $result.status; exitCode = $result.exitCode; reason = $result.reason
        }

        if ($result.status -eq "restart") { continue }
        return $result
    }

    return [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = "restart_exhausted" }
}

# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

function Parse-IssueList([string] $Raw) {
    if ([string]::IsNullOrWhiteSpace($Raw)) { return @() }
    $parts = $Raw -split '[,;\s]+' | Where-Object { $_ -match '^\d+$' }
    return @($parts | ForEach-Object { [int]$_ })
}

function Invoke-Doctor {
    param([string] $RepoRoot, $Config)
    $ok = $true
    Write-Host "repoRoot: $RepoRoot"
    Write-Host "defaultModel (skill): $script:DefaultModel"
    Write-Host "config.model: $($Config.model)"
    Write-Host "modelFormat: $(if (Test-ModelIdFormat ([string]$Config.model)) { 'ok' } else { 'INVALID' })"
    if (-not (Test-ModelIdFormat ([string]$Config.model))) { $ok = $false }

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $o = & gh auth status 2>&1 | Out-String
    $ErrorActionPreference = $prev
    if ($LASTEXITCODE -eq 0) { Write-Host "gh: ok" }
    else { Write-Host "gh: FAIL — $o" -ForegroundColor Red; $ok = $false }

    try {
        $agent = Resolve-CursorAgent -Override ([string]$Config.agentPath)
        Write-Host "agent: $agent"
        $verPsiOut = & $agent --version 2>&1 | Out-String
        Write-Host "agent version: $($verPsiOut.Trim())"
        if ($agent -match '[\\/]\.grok[\\/]') {
            Write-Host "agent: FAIL — grok binary" -ForegroundColor Red
            $ok = $false
        }
        if (Test-AgentAuth -AgentPath $agent) {
            Write-Host "agent auth: ok"
            $models = Get-AgentModelIds -AgentPath $agent
            if ($models.ok) {
                $hit = $models.ids -contains [string]$Config.model
                if ($hit) {
                    Write-Host "model list: FOUND $($Config.model)" -ForegroundColor Green
                } else {
                    Write-Host "model list: $($Config.model) NOT found in --list-models (check slug / account access)" -ForegroundColor Yellow
                    Write-Host $models.raw
                    # Soft-fail: slug may still work if list parsing is incomplete
                }
            } else {
                Write-Host "model list: could not fetch — $($models.raw)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "agent auth: NOT LOGGED IN — run: `"$agent`" login   or set CURSOR_API_KEY" -ForegroundColor Yellow
            Write-Host "note: cannot live-verify model against --list-models until login; format check + published slug $script:DefaultModel used." -ForegroundColor Yellow
            $ok = $false
        }
    } catch {
        Write-Host "agent: FAIL — $_" -ForegroundColor Red
        $ok = $false
    }

    Write-Host "note: $script:DefaultModelNote"
    if ($ok) { Write-Host "doctor: PASS" -ForegroundColor Green; return 0 }
    Write-Host "doctor: FAIL" -ForegroundColor Red
    return 1
}

function Invoke-Plan {
    param($Queue, [bool] $AsJson, [string] $Model)
    if ($AsJson) {
        $payload = [ordered]@{ model = $Model; queue = $Queue }
        [Console]::Out.WriteLine(($payload | ConvertTo-Json -Depth 6))
        return
    }
    Write-Host "batch model: $Model"
    if (-not $Queue -or $Queue.Count -eq 0) { Write-Host "queue: (empty)"; return }
    Write-Host "queue ($($Queue.Count)):"
    $i = 1
    foreach ($it in $Queue) {
        $labs = if ($it.labels) { ($it.labels -join ",") } else { "-" }
        Write-Host ("  {0,2}. #{1}  [{2}]  {3}  labels={4}" -f $i, $it.number, $it.state, $it.title, $labs)
        $i++
    }
}

function Invoke-Run {
    param(
        [string] $RepoRoot,
        $Config,
        $Queue,
        [bool] $Once,
        [string] $ModelOverride,
        [Nullable[bool]] $StrictCloseOverride
    )

    if (-not $Queue -or $Queue.Count -eq 0) {
        Write-Host "Nothing to run."
        return 0
    }

    $agent = Resolve-CursorAgent -Override ([string]$Config.agentPath)
    if (-not (Test-AgentAuth -AgentPath $agent)) {
        throw "Cursor Agent not authenticated. Run: `"$agent`" login   or set CURSOR_API_KEY"
    }

    $model = if ($ModelOverride) { $ModelOverride } else { [string]$Config.model }
    if ([string]::IsNullOrWhiteSpace($model)) { $model = $script:DefaultModel }
    if (-not (Test-ModelIdFormat $model)) { throw "Invalid model id format: $model" }

    Write-Host "batch model: $model" -ForegroundColor Cyan

    $strict = if ($null -ne $StrictCloseOverride) { [bool]$StrictCloseOverride } else { [bool]$Config.strictClose }
    $template = [string]$Config.promptTemplate
    if ([string]::IsNullOrWhiteSpace($template)) { $template = "/implement #{number}" }

    $toRun = if ($Once) { @($Queue[0]) } else { @($Queue) }
    $maxLogs = [int]$Config.maxMachineLogRuns
    if ($maxLogs -le 0) { $maxLogs = 3 }

    $runDir = New-MachineRun -RepoRoot $RepoRoot -MaxRuns $maxLogs -Model $model -QueueNumbers @($toRun | ForEach-Object { $_.number })
    Write-Host "machine log run: $runDir (rotating max=$maxLogs)" 

    $state = [ordered]@{
        startedAt  = Get-IsoNow
        workspace  = $RepoRoot
        model      = $model
        once       = $Once
        runDir     = $runDir
        queue      = @($toRun | ForEach-Object { $_.number })
        completed  = @()
        failed     = $null
        paused     = $null
        current    = $null
        finishedAt = $null
    }
    Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state

    foreach ($it in $toRun) {
        $n = [int]$it.number
        $state.current = $n
        Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state

        Write-Host ""
        Write-Host "======== ticket #$n — $($it.title) ========" -ForegroundColor Green

        $ticketDir = Join-Path $runDir ("tickets\{0}" -f $n)
        New-Item -ItemType Directory -Path $ticketDir -Force | Out-Null

        $before = [string]$Config.hooks.beforeTicket
        Invoke-Hook -CommandLine $before -RepoRoot $RepoRoot -Number $n -Phase "before"

        $prompt = Format-Prompt -Template $template -Number $n -Title $it.title
        $result = Invoke-SupervisedTicket `
            -AgentPath $agent `
            -RepoRoot $RepoRoot `
            -Config $Config `
            -Model $model `
            -IssueNumber $n `
            -Title $it.title `
            -UserPrompt $prompt `
            -TicketDir $ticketDir

        $after = [string]$Config.hooks.afterTicket
        try { Invoke-Hook -CommandLine $after -RepoRoot $RepoRoot -Number $n -Phase "after" } catch {
            Write-Host "[warn] afterTicket hook: $_" -ForegroundColor Yellow
        }

        if ($result.status -eq "paused_user") {
            $state.paused = [ordered]@{ number = $n; reason = $result.reason; at = Get-IsoNow; ticketDir = $ticketDir }
            $state.finishedAt = Get-IsoNow
            Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state
            Write-JsonFileAtomic (Join-Path $runDir "run.json") ([ordered]@{
                status = "paused_user"; pausedIssue = $n; reason = $result.reason; path = $runDir; model = $model
            })
            Write-Host "PAUSED awaiting human: #$n ($($result.reason))" -ForegroundColor Red
            Write-Host "Inspect: $ticketDir" -ForegroundColor Yellow
            return 3
        }

        if ($result.status -ne "finished" -or $result.exitCode -ne 0) {
            $state.failed = [ordered]@{ number = $n; exitCode = $result.exitCode; reason = $result.reason; at = Get-IsoNow; ticketDir = $ticketDir }
            $state.finishedAt = Get-IsoNow
            Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state
            Write-Host "FAIL: #$n status=$($result.status) exit=$($result.exitCode) reason=$($result.reason)" -ForegroundColor Red
            return 2
        }

        $meta = Get-IssueMeta -Number $n
        if ($meta.state -ne "CLOSED") {
            $msg = "Issue #$n still OPEN after agent exit 0."
            if ($strict) {
                $state.failed = [ordered]@{ number = $n; reason = "strict-close"; at = Get-IsoNow }
                $state.finishedAt = Get-IsoNow
                Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state
                Write-Host "FAIL: $msg" -ForegroundColor Red
                return 2
            }
            Write-Host "WARN: $msg Continuing." -ForegroundColor Yellow
        } else {
            Write-Host "OK: #$n closed." -ForegroundColor Green
        }

        $state.completed = @($state.completed) + @($n)
        $state.current = $null
        Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state
    }

    $state.finishedAt = Get-IsoNow
    Write-JsonFileAtomic (Get-StatePath $RepoRoot) $state
    Write-JsonFileAtomic (Join-Path $runDir "run.json") ([ordered]@{
        status = "completed"; completed = $state.completed; path = $runDir; model = $model; finishedAt = $state.finishedAt
    })
    Write-Host ""
    Write-Host "Done. Completed: $($state.completed -join ', ')" -ForegroundColor Green
    return 0
}

function Invoke-Status {
    param([string] $RepoRoot, [bool] $AsJson)
    $path = Get-StatePath $RepoRoot
    if (-not (Test-Path -LiteralPath $path)) {
        if ($AsJson) { [Console]::Out.WriteLine("{}") } else { Write-Host "No state yet ($path)" }
        return 0
    }
    $st = Read-JsonFile $path
    $json = ($st | ConvertTo-Json -Depth 10)
    if ($AsJson) { [Console]::Out.WriteLine($json) } else { Write-Host $json }
    return 0
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

function Main {
    $cmd = $Command.Trim().ToLowerInvariant()
    if (-not $cmd -or $cmd -in @("help", "-h", "--help")) { Show-Help; return 0 }

    $workspaceOpt = Get-FlagValue -ArgsList $Rest -Names @("--workspace")
    $repoRoot = Find-RepoRoot -Start $workspaceOpt
    if (-not $repoRoot) {
        Write-Host "Could not find repo root (.git). Pass --workspace <path>." -ForegroundColor Red
        return 1
    }

    $configPath = Get-FlagValue -ArgsList $Rest -Names @("--config")
    $config = Load-Config -RepoRoot $repoRoot -ExplicitPath $configPath

    $asJson = Test-HasFlag -ArgsList $Rest -Names @("--json")
    $parentRaw = Get-FlagValue -ArgsList $Rest -Names @("--parent")
    $issuesRaw = Get-FlagValue -ArgsList $Rest -Names @("--issues")
    $label = Get-FlagValue -ArgsList $Rest -Names @("--label")
    $fromRaw = Get-FlagValue -ArgsList $Rest -Names @("--from")
    $model = Get-FlagValue -ArgsList $Rest -Names @("--model")
    $agentOverride = Get-FlagValue -ArgsList $Rest -Names @("--agent")
    $promptTemplate = Get-FlagValue -ArgsList $Rest -Names @("--prompt-template")
    $once = Test-HasFlag -ArgsList $Rest -Names @("--once")
    $dryRun = Test-HasFlag -ArgsList $Rest -Names @("--dry-run")
    $strictClose = Test-HasFlag -ArgsList $Rest -Names @("--strict-close")
    $continueIfOpen = Test-HasFlag -ArgsList $Rest -Names @("--continue-if-open")
    $noForce = Test-HasFlag -ArgsList $Rest -Names @("--no-force")

    if ($agentOverride) { $config.agentPath = $agentOverride }
    if ($promptTemplate) { $config.promptTemplate = $promptTemplate }
    if ($noForce) { $config.force = $false }

    $parent = $null
    if ($parentRaw) { $parent = [int]$parentRaw }
    $from = $null
    if ($fromRaw) { $from = [int]$fromRaw }
    $issues = @(Parse-IssueList $issuesRaw)

    if (-not $label -and -not $parent -and $issues.Count -eq 0) {
        $cfgParent = $config.defaultParent
        if ($null -ne $cfgParent -and "$cfgParent" -ne "" -and [int]$cfgParent -gt 0) {
            $parent = [int]$cfgParent
        } else {
            $label = [string]$config.readyLabel
        }
    }
    if ($parent -or $issues.Count -gt 0) { $label = $null }

    $effectiveModel = if ($model) { $model } else { [string]$config.model }

    try {
        switch ($cmd) {
            "doctor" { return Invoke-Doctor -RepoRoot $repoRoot -Config $config }
            "status" { return Invoke-Status -RepoRoot $repoRoot -AsJson $asJson }
            "plan" {
                $queue = Resolve-Queue -Parent $parent -Issues $issues -Label $label -From $from
                Invoke-Plan -Queue $queue -AsJson $asJson -Model $effectiveModel
                return 0
            }
            "run" {
                if ($dryRun) {
                    $queue = Resolve-Queue -Parent $parent -Issues $issues -Label $label -From $from
                    Invoke-Plan -Queue $queue -AsJson $asJson -Model $effectiveModel
                    return 0
                }
                $queue = Resolve-Queue -Parent $parent -Issues $issues -Label $label -From $from
                Invoke-Plan -Queue $queue -AsJson:$false -Model $effectiveModel
                $strictOverride = $null
                if ($strictClose) { $strictOverride = $true }
                if ($continueIfOpen) { $strictOverride = $false }
                return Invoke-Run `
                    -RepoRoot $repoRoot `
                    -Config $config `
                    -Queue $queue `
                    -Once $once `
                    -ModelOverride $model `
                    -StrictCloseOverride $strictOverride
            }
            default {
                Write-Host "Unknown command: $cmd" -ForegroundColor Red
                Show-Help
                return 1
            }
        }
    } catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        return 1
    }
}

$script:ExitCode = Main
exit $script:ExitCode
