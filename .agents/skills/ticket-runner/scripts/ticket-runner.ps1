#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string] $Command = "",
    [Parameter(ValueFromRemainingArguments = $true)] [string[]] $Rest = @()
)

$ErrorActionPreference = "Stop"
$script:SkillRoot = Split-Path -Parent $PSScriptRoot
$script:DefaultModel = "deepseek/deepseek-v4-flash"
$script:FallbackModel = "alibaba/qwen3.7-max"

function Get-FlagValue {
    param([string[]] $ArgsList, [string[]] $Names, [string] $Default = $null)
    for ($i = 0; $i -lt $ArgsList.Count; $i++) {
        foreach ($name in $Names) {
            if ($ArgsList[$i] -eq $name -and $i + 1 -lt $ArgsList.Count) { return $ArgsList[$i + 1] }
            $prefix = "$name="
            if ($ArgsList[$i].StartsWith($prefix)) { return $ArgsList[$i].Substring($prefix.Length) }
        }
    }
    return $Default
}

function Test-HasFlag {
    param([string[]] $ArgsList, [string[]] $Names)
    foreach ($arg in $ArgsList) { foreach ($name in $Names) { if ($arg -eq $name) { return $true } } }
    return $false
}

function Test-FlagSpecified {
    param([string[]] $ArgsList, [string[]] $Names)
    foreach ($arg in $ArgsList) {
        foreach ($name in $Names) {
            if ($arg -eq $name -or $arg.StartsWith("$name=")) { return $true }
        }
    }
    return $false
}

function Show-Help {
    @"
ticket-runner - serial OpenCode landing for GitHub Issues

Usage:
  ticket-runner.ps1 <doctor|plan|run|status|help> [options]

Queue:
  --parent <n>           Parent Spec task list, then tracked Issues
  --issues <a,b,c>       Explicit ordered issue numbers
  --label <name>         Open Issues with a label
  --from <n>             Start at this issue in the resolved queue

Run:
  --once                 Run only the first pending ticket
  --model <id>           Primary batch model override
  --workspace <path>     Repository root
  --config <path>        Runner config JSON
  --strict-close         Fail when a successful worker leaves the Issue open
  --dry-run              Resolve and print the queue only
  --json                 Machine-readable plan/status output
"@ | Write-Host
}

function Find-RepoRoot {
    param([string] $Start)
    $dir = if ($Start) { (Resolve-Path -LiteralPath $Start).Path } else { (Get-Location).Path }
    for ($i = 0; $i -lt 12; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dir ".git")) { return $dir }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

function Get-StateDir([string] $Root) { Join-Path $Root ".opencode\ticket-runner" }
function Get-RunsDir([string] $Root) { Join-Path (Get-StateDir $Root) "runs" }
function Get-StatePath([string] $Root) { Join-Path (Get-StateDir $Root) "state.json" }
function Get-RunsIndexPath([string] $Root) { Join-Path (Get-RunsDir $Root) "index.json" }

function Ensure-Dir([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
}

function Get-IsoNow { [DateTime]::UtcNow.ToString("o") }

function Read-JsonFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $raw = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return $raw | ConvertFrom-Json
}

function Write-JsonAtomic([string] $Path, $Object) {
    Ensure-Dir (Split-Path -Parent $Path)
    $tmp = "$Path.$([guid]::NewGuid().ToString('N')).tmp"
    $json = $Object | ConvertTo-Json -Depth 20
    try {
        [System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $tmp -Destination $Path -Force
    } finally {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
}

function Append-Event([string] $Path, [string] $Type, [hashtable] $Fields = @{}) {
    try {
        Ensure-Dir (Split-Path -Parent $Path)
        $payload = [ordered]@{ ts = Get-IsoNow; type = $Type }
        foreach ($key in $Fields.Keys) { $payload[$key] = $Fields[$key] }
        [System.IO.File]::AppendAllText($Path, (($payload | ConvertTo-Json -Compress -Depth 10) + [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    } catch { Write-Host "[warn] event log failed: $Path :: $_" -ForegroundColor Yellow }
}

function Get-DefaultConfig {
    [ordered]@{
        model = $script:DefaultModel
        fallbackModel = $script:FallbackModel
        opencodeAgent = "ticket-implementer"
        doctorAgent = "ticket-doctor"
        promptTemplate = "Implement GitHub issue #{number}."
        strictClose = $false
        readyLabel = "ready-for-agent"
        defaultParent = 114
        ticketBudgetMinutes = 30
        stdoutIdleMinutes = 10
        parseErrorLimit = 8
        interventionBudgetMinutes = 30
        interventionGraceMinutes = 15
        pollSeconds = 5
        maxMachineLogRuns = 3
        hooks = [ordered]@{ beforeTicket = ""; afterTicket = "" }
    }
}

function Merge-Config($Base, $Overlay) {
    if (-not $Overlay) { return $Base }
    $out = [ordered]@{}
    foreach ($key in $Base.Keys) { $out[$key] = $Base[$key] }
    foreach ($prop in $Overlay.PSObject.Properties) {
        if ($prop.Name -eq "hooks" -and $prop.Value) {
            $hooks = [ordered]@{}
            foreach ($key in $out.hooks.Keys) { $hooks[$key] = $out.hooks[$key] }
            foreach ($hook in $prop.Value.PSObject.Properties) { $hooks[$hook.Name] = [string]$hook.Value }
            $out.hooks = $hooks
        } else { $out[$prop.Name] = $prop.Value }
    }
    return $out
}

function Load-Config {
    param([string] $Root, [string] $ExplicitPath)
    $config = Get-DefaultConfig
    $example = Join-Path $script:SkillRoot "config.example.json"
    if (Test-Path -LiteralPath $example) { $config = Merge-Config $config (Read-JsonFile $example) }
    $project = Join-Path $Root ".opencode\ticket-runner.config.json"
    if (Test-Path -LiteralPath $project) { $config = Merge-Config $config (Read-JsonFile $project) }
    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) { throw "Config not found: $ExplicitPath" }
        $config = Merge-Config $config (Read-JsonFile $ExplicitPath)
    }
    return $config
}

function Test-ModelId([string] $Model) { return ($Model -match '^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$') }

function Invoke-GhJson {
    param([string[]] $ArgsList)
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) { throw "GitHub CLI (gh) not found." }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $gh.Source
    $quoted = foreach ($arg in $ArgsList) {
        $value = [string]$arg
        if ($value -match '[\s"]') { '"' + $value.Replace('"', '\"') + '"' } else { $value }
    }
    $psi.Arguments = ($quoted -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "gh failed ($($process.ExitCode)): $stderr$stdout" }
    if ([string]::IsNullOrWhiteSpace($stdout)) { return $null }
    return $stdout.Trim() | ConvertFrom-Json
}

function Get-IssueMeta([int] $Number) { Invoke-GhJson @("issue", "view", "$Number", "--json", "number,title,state,labels") }

function Get-TaskNumbers([string] $Body) {
    $result = New-Object System.Collections.Generic.List[int]
    $seen = @{}
    if ([string]::IsNullOrWhiteSpace($Body)) { return @() }
    foreach ($match in ([regex]'(?m)^\s*-\s*\[(?: |x|X)\]\s*#(\d+)\b').Matches($Body)) {
        $number = [int]$match.Groups[1].Value
        if (-not $seen.ContainsKey($number)) { $seen[$number] = $true; $result.Add($number) | Out-Null }
    }
    return @($result)
}

function Get-TrackedNumbers([int] $Parent) {
    try {
        $repo = Invoke-GhJson @("repo", "view", "--json", "owner,name")
        $query = 'query($o:String!,$n:String!,$num:Int!){repository(owner:$o,name:$n){issue(number:$num){trackedIssues(first:50){nodes{number state}}}}}'
        $response = Invoke-GhJson @("api", "graphql", "-f", "query=$query", "-F", "o=$($repo.owner.login)", "-F", "n=$($repo.name)", "-F", "num=$Parent")
        return @($response.data.repository.issue.trackedIssues.nodes | ForEach-Object { [int]$_.number })
    } catch { return @() }
}

function Resolve-Queue {
    param([Nullable[int]] $Parent, [int[]] $Issues, [string] $Label, [Nullable[int]] $From)
    $ordered = New-Object System.Collections.Generic.List[int]
    if ($Issues -and $Issues.Count -gt 0) {
        foreach ($number in $Issues) { $ordered.Add([int]$number) | Out-Null }
    } elseif ($Parent) {
        $parentMeta = Invoke-GhJson @("issue", "view", "$Parent", "--json", "body")
        $taskNumbers = Get-TaskNumbers ([string]$parentMeta.body)
        $tracked = if ($taskNumbers.Count -eq 0) { Get-TrackedNumbers $Parent } else { @() }
        $source = if ($taskNumbers.Count -gt 0) { $taskNumbers } else { $tracked }
        if ($source.Count -eq 0) { throw "Parent #$Parent has no task-list children and no tracked Issues." }
        foreach ($number in $source) { $ordered.Add([int]$number) | Out-Null }
    } elseif ($Label) {
        $items = Invoke-GhJson @("issue", "list", "--state", "open", "--label", $Label, "--limit", "100", "--json", "number")
        foreach ($item in ($items | Sort-Object number)) { $ordered.Add([int]$item.number) | Out-Null }
    } else { throw "Provide --parent, --issues, or --label." }

    if ($From) {
        $index = $ordered.IndexOf([int]$From)
        if ($index -lt 0) { throw "--from #$From is not in the queue." }
        $ordered = [System.Collections.Generic.List[int]]@($ordered.GetRange($index, $ordered.Count - $index))
    }

    $queue = @()
    foreach ($number in $ordered) {
        $meta = Get-IssueMeta $number
        if ([string]$meta.state -eq "CLOSED") { continue }
        $labels = @($meta.labels | ForEach-Object { [string]$_.name })
        $queue += [pscustomobject]@{ number = [int]$meta.number; title = [string]$meta.title; state = [string]$meta.state; labels = $labels }
    }
    return $queue
}

function Format-Prompt([string] $Template, [int] $Number, [string] $Title) {
    return $Template.Replace("#{number}", "#$Number").Replace("{number}", "$Number").Replace("{title}", $Title)
}

function Build-WorkerPrompt([string] $Task, [int] $Number, [string] $Title) {
    @"
[ticket-runner headless worker]
Implement exactly GitHub issue #${Number}: $Title

$Task

Rules:
- Work only on this Issue. Do not start another ticket or use a git worktree.
- Do not ask the user a question. If a product or technical decision is missing, preserve the workspace and finish with TICKET_STATUS: NEEDS_HUMAN plus the exact decision needed.
- Follow AGENTS.md, the repository's Unity CLI rules, and the ai-workspace claim/test mutex.
- Inspect the diff and run the relevant verification before reporting completion.
- Do not push, reset hard, clean the repository, close unrelated Issues, or merge anything.

The final response must contain exactly one status line:
TICKET_STATUS: IMPLEMENTED
TICKET_STATUS: NEEDS_HUMAN
or
TICKET_STATUS: FAILED
"@
}

function Escape-WinArg([string] $Value) {
    if ($null -eq $Value) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    $escaped = (($Value -replace '(\\*)"','$1$1\"') -replace '(\\+)$','$1$1')
    return '"' + $escaped + '"'
}

function Join-WinArgs([string[]] $ArgsList) { return (($ArgsList | ForEach-Object { Escape-WinArg $_ }) -join ' ') }

function Get-OpenCodePath {
    $command = Get-Command opencode -ErrorAction SilentlyContinue
    if (-not $command) { throw "OpenCode CLI not found. Install it or add opencode to PATH." }
    return [string]$command.Source
}

function Stop-ProcessTree([int] $ProcessId) {
    if ($ProcessId -le 0) { return }
    try { cmd.exe /c "taskkill /PID $ProcessId /T /F" | Out-Null } catch { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue }
}

function Start-OpenCode {
    param([string] $OpenCodePath, [string] $Root, [string] $Agent, [string] $Model, [string] $Prompt, [string] $Stdout, [string] $Stderr)
    Ensure-Dir (Split-Path -Parent $Stdout)
    [System.IO.File]::WriteAllText($Stdout, "", [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($Stderr, "", [System.Text.UTF8Encoding]::new($false))
    $args = @("run", "--dir", $Root, "--agent", $Agent, "--model", $Model, "--format", "json", "--auto", $Prompt)
    $process = Start-Process -FilePath $OpenCodePath -ArgumentList (Join-WinArgs $args) -WorkingDirectory $Root -WindowStyle Hidden -PassThru -RedirectStandardOutput $Stdout -RedirectStandardError $Stderr
    [pscustomobject]@{
        Process = $process
        Args = (Join-WinArgs $args)
        Stdout = $Stdout
        Stderr = $Stderr
        LastSize = 0L
        LastOutputUtc = [DateTime]::UtcNow
        ParseErrors = 0
        ProviderError = $false
        Status = $null
        LastStdoutText = ""
        LastStderrText = ""
        LastStdoutRemainder = ""
        LastStderrRemainder = ""
    }
}

function Update-ProcessSignals {
    param($Handle, [switch] $Flush)
    $total = 0L
    foreach ($path in @($Handle.Stdout, $Handle.Stderr)) {
        if (Test-Path -LiteralPath $path) { $total += (Get-Item -LiteralPath $path).Length }
    }
    if ($total -gt $Handle.LastSize) { $Handle.LastSize = $total; $Handle.LastOutputUtc = [DateTime]::UtcNow }
    $newText = ""
    foreach ($entry in @(
        [pscustomobject]@{ Path = $Handle.Stdout; Property = "LastStdoutText"; Remainder = "LastStdoutRemainder" },
        [pscustomobject]@{ Path = $Handle.Stderr; Property = "LastStderrText"; Remainder = "LastStderrRemainder" }
    )) {
        if (-not (Test-Path -LiteralPath $entry.Path)) { continue }
        $text = [System.IO.File]::ReadAllText($entry.Path, [System.Text.Encoding]::UTF8)
        $old = [string]$Handle.($entry.Property)
        $delta = if ($old -and $text.StartsWith($old)) { $text.Substring($old.Length) } else { $text }
        $Handle.($entry.Property) = $text
        $combined = [string]$Handle.($entry.Remainder) + $delta
        $lines = @($combined -split "`r?`n", -1)
        $endsWithNewline = $combined -match "`r?`n$"
        if ($endsWithNewline) { $Handle.($entry.Remainder) = "" }
        elseif ($lines.Count -gt 0) {
            $Handle.($entry.Remainder) = [string]$lines[$lines.Count - 1]
            if ($lines.Count -gt 1) { $lines = @($lines[0..($lines.Count - 2)]) } else { $lines = @() }
        }
        foreach ($line in $lines) { $newText += "`n" + $line }
        if ($Flush -and $Handle.($entry.Remainder)) {
            $newText += "`n" + [string]$Handle.($entry.Remainder)
            $Handle.($entry.Remainder) = ""
        }
    }
    if ($newText -match '(?i)TICKET_STATUS:\s*(IMPLEMENTED|NEEDS_HUMAN|FAILED)') { $Handle.Status = $Matches[1].ToUpperInvariant() }
    if ($newText -match '(?i)(model not found|provider.*(unavailable|error|failed|timeout)|rate limit|billing limit|authentication required|invalid api key|unauthorized.*(provider|deepseek|alibaba|opencode)|upstream.*5\d\d)') { $Handle.ProviderError = $true }
    foreach ($line in ($newText -split "`r?`n")) {
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith("{") -and $trimmed.EndsWith("}")) {
            try { $null = $trimmed | ConvertFrom-Json } catch { $Handle.ParseErrors = [int]$Handle.ParseErrors + 1 }
        }
    }
}

function Close-OpenCode($Handle) {
    if (-not $Handle) { return }
    try {
        if (-not $Handle.Process.HasExited) { Stop-ProcessTree $Handle.Process.Id; $Handle.Process.WaitForExit(5000) | Out-Null }
    } catch {}
    try { $Handle.Process.Dispose() } catch {}
}

function Add-OpenCodeText {
    param($Value, [System.Collections.Generic.List[string]] $Parts)
    if ($null -eq $Value) { return }
    if ($Value -is [string]) { $Parts.Add([string]$Value) | Out-Null; return }
    if ($Value -is [System.Collections.IEnumerable]) {
        foreach ($item in $Value) { Add-OpenCodeText -Value $item -Parts $Parts }
        return
    }
    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -in @("text", "result", "content", "message", "output", "part")) {
            Add-OpenCodeText -Value $property.Value -Parts $Parts
        }
    }
}

function Get-OpenCodeText([string] $Raw) {
    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($Raw -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { Add-OpenCodeText -Value ($line | ConvertFrom-Json) -Parts $parts }
        catch { $parts.Add($line) | Out-Null }
    }
    return ($parts -join "`n")
}

function Invoke-DoctorDecision {
    param([string] $OpenCodePath, [string] $Root, $Config, [string] $TicketDir, [int] $Issue, [string] $Reason, [string] $EventsPath)
    $dir = Join-Path $TicketDir "intervention"
    Ensure-Dir $dir
    $stdout = Join-Path $dir "stdout.log"
    $stderr = Join-Path $dir "stderr.log"
    $prompt = @"
[ticket-runner doctor]
Issue #$Issue is supervised by a parent process and is unhealthy: $Reason
Read only these local artifacts under ${TicketDir}: stdout.log, stderr.log, events.ndjson, meta.json, and intervention logs if present.
Do not edit files, implement code, ask questions, or start subagents.
Return exactly one line with JSON:
DOCTOR_ACTION: {"action":"kill_restart"|"continue_wait"|"pause_user","reason":"short evidence-based reason"}
Choose continue_wait only when logs prove real progress. Choose pause_user when state is ambiguous.
"@
    Append-Event $EventsPath "intervention_start" @{ reason = $Reason }
    $handle = Start-OpenCode -OpenCodePath $OpenCodePath -Root $Root -Agent ([string]$Config.doctorAgent) -Model ([string]$Config.fallbackModel) -Prompt $prompt -Stdout $stdout -Stderr $stderr
    $deadline = [DateTime]::UtcNow.AddMinutes([int]$Config.interventionBudgetMinutes)
    try {
        while (-not $handle.Process.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Seconds ([Math]::Max(1, [int]$Config.pollSeconds)) }
        if (-not $handle.Process.HasExited) { Stop-ProcessTree $handle.Process.Id; Append-Event $EventsPath "intervention_timeout" @{} }
        $handle.Process.WaitForExit(3000) | Out-Null
    } finally { Close-OpenCode $handle }
    $raw = if (Test-Path -LiteralPath $stdout) { [System.IO.File]::ReadAllText($stdout, [System.Text.Encoding]::UTF8) } else { "" }
    $eventText = Get-OpenCodeText $raw
    $match = [regex]::Match($eventText, 'DOCTOR_ACTION:\s*(\{[^\r\n]*\})')
    if (-not $match.Success) { Append-Event $EventsPath "intervention_no_decision" @{}; return "pause_user" }
    try {
        $decision = $match.Groups[1].Value | ConvertFrom-Json
        $action = [string]$decision.action
        if ($action -notin @("kill_restart", "continue_wait", "pause_user")) { return "pause_user" }
        Append-Event $EventsPath "intervention_decision" @{ action = $action; reason = [string]$decision.reason }
        return $action
    } catch { Append-Event $EventsPath "intervention_parse_error" @{ error = "$_" }; return "pause_user" }
}

function Invoke-SupervisedTicket {
    param([string] $OpenCodePath, [string] $Root, $Config, [string] $PrimaryModel, [int] $Issue, [string] $Title, [string] $Task, [string] $TicketDir)
    $events = Join-Path $TicketDir "events.ndjson"
    $stdout = Join-Path $TicketDir "stdout.log"
    $stderr = Join-Path $TicketDir "stderr.log"
    $meta = Join-Path $TicketDir "meta.json"
    $intervened = $false
    $attempt = 0
    $model = $PrimaryModel
    $useFallback = $false
    $graceDeadline = $null
    while ($attempt -lt 2) {
        $attempt++
        if ($useFallback -and $Config.fallbackModel) { $model = [string]$Config.fallbackModel } else { $model = $PrimaryModel }
        $prompt = Build-WorkerPrompt -Task $Task -Number $Issue -Title $Title
        $handle = Start-OpenCode -OpenCodePath $OpenCodePath -Root $Root -Agent ([string]$Config.opencodeAgent) -Model $model -Prompt $prompt -Stdout $stdout -Stderr $stderr
        Write-JsonAtomic $meta ([ordered]@{ issue = $Issue; title = $Title; model = $model; attempt = $attempt; startedAt = Get-IsoNow; status = "running" })
        Append-Event $events "process_started" @{ pid = $handle.Process.Id; attempt = $attempt; model = $model; args = $handle.Args }
        $deadline = [DateTime]::UtcNow.AddMinutes([int]$Config.ticketBudgetMinutes)
        $result = $null
        try {
            while (-not $handle.Process.HasExited) {
                Start-Sleep -Seconds ([Math]::Max(1, [int]$Config.pollSeconds))
                Update-ProcessSignals -Handle $handle
                $idle = ([DateTime]::UtcNow - $handle.LastOutputUtc).TotalSeconds
                $reason = $null
                if ($handle.Status -eq "NEEDS_HUMAN") { $reason = "worker_needs_human" }
                elseif ([int]$handle.ParseErrors -ge [int]$Config.parseErrorLimit) { $reason = "json_parse_errors" }
                elseif ($idle -ge ([int]$Config.stdoutIdleMinutes * 60)) { $reason = "stdout_idle" }
                elseif ([DateTime]::UtcNow -gt $deadline) { $reason = "ticket_budget_exceeded" }
                elseif ($graceDeadline -and [DateTime]::UtcNow -gt $graceDeadline) { $reason = "continue_wait_grace_exceeded" }
                if (-not $reason) { continue }
                $handle.Process.Refresh()
                if ($handle.Process.HasExited) { break }
                Append-Event $events "unhealthy" @{ reason = $reason; pid = $handle.Process.Id; idleSeconds = [int]$idle }
                if ($reason -eq "worker_needs_human" -or $reason -eq "continue_wait_grace_exceeded" -or $intervened) {
                    Stop-ProcessTree $handle.Process.Id
                    $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = $reason }
                    break
                }
                $intervened = $true
                $action = Invoke-DoctorDecision -OpenCodePath $OpenCodePath -Root $Root -Config $Config -TicketDir $TicketDir -Issue $Issue -Reason $reason -EventsPath $events
                if ($action -eq "continue_wait") {
                    $graceDeadline = [DateTime]::UtcNow.AddMinutes([int]$Config.interventionGraceMinutes)
                    $handle.LastOutputUtc = [DateTime]::UtcNow
                    continue
                }
                if ($action -eq "kill_restart") {
                    if ($handle.ProviderError) { $useFallback = $true }
                    Stop-ProcessTree $handle.Process.Id
                    $handle.Process.WaitForExit(5000) | Out-Null
                    $archive = Join-Path $TicketDir ("attempt-{0}" -f $attempt)
                    Ensure-Dir $archive
                    Copy-Item -LiteralPath $stdout -Destination (Join-Path $archive "stdout.log") -Force -ErrorAction SilentlyContinue
                    Copy-Item -LiteralPath $stderr -Destination (Join-Path $archive "stderr.log") -Force -ErrorAction SilentlyContinue
                    $result = [pscustomobject]@{ status = "restart"; exitCode = -1; reason = $reason }
                    break
                }
                Stop-ProcessTree $handle.Process.Id
                $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = "doctor_pause_user" }
                break
            }
            if (-not $result) {
                Update-ProcessSignals -Handle $handle -Flush
                $handle.Process.Refresh()
                $handle.Process.WaitForExit(3000) | Out-Null
                $code = [int]$handle.Process.ExitCode
                if ($handle.Status -eq "NEEDS_HUMAN") { $result = [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = "worker_needs_human" } }
                elseif ($handle.Status -eq "FAILED" -or $code -ne 0) { $result = [pscustomobject]@{ status = "failed"; exitCode = $code; reason = $(if ($handle.ProviderError) { "provider_error" } else { "worker_failed" }) } }
                elseif ($handle.Status -eq "IMPLEMENTED") { $result = [pscustomobject]@{ status = "finished"; exitCode = 0; reason = "worker_implemented" } }
                else { $result = [pscustomobject]@{ status = "failed"; exitCode = 1; reason = "missing_ticket_status" } }
            }
        } finally {
            Close-OpenCode $handle
            if (-not $result) { $result = [pscustomobject]@{ status = "failed"; exitCode = 1; reason = "supervisor_error" } }
            Write-JsonAtomic $meta ([ordered]@{ issue = $Issue; title = $Title; model = $model; attempt = $attempt; finishedAt = Get-IsoNow; status = $result.status; exitCode = $result.exitCode; reason = $result.reason })
            Append-Event $events "attempt_end" @{ status = $result.status; exitCode = $result.exitCode; reason = $result.reason }
        }
        if ($result.status -eq "restart") { continue }
        if ($result.status -eq "failed" -and $result.reason -eq "provider_error" -and $attempt -lt 2) { $useFallback = $true; continue }
        return $result
    }
    return [pscustomobject]@{ status = "paused_user"; exitCode = 3; reason = "restart_exhausted" }
}

function Invoke-Hook([string] $CommandLine, [string] $Root, [int] $Issue, [string] $Phase) {
    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return }
    Push-Location $Root
    try {
        $env:TICKET_RUNNER_ISSUE = "$Issue"
        $env:TICKET_RUNNER_PHASE = $Phase
        $env:TICKET_RUNNER_ROOT = $Root
        cmd.exe /c $CommandLine
        if ($LASTEXITCODE -ne 0) { throw "Hook failed ($Phase), exit=$LASTEXITCODE" }
    } finally {
        Pop-Location
        Remove-Item Env:TICKET_RUNNER_ISSUE,Env:TICKET_RUNNER_PHASE,Env:TICKET_RUNNER_ROOT -ErrorAction SilentlyContinue
    }
}

function New-Run([string] $Root, [int] $MaxRuns, [string] $Model, $Queue) {
    Ensure-Dir (Get-RunsDir $Root)
    $indexPath = Get-RunsIndexPath $Root
    $index = Read-JsonFile $indexPath
    $runs = if ($index -and $index.runs) { @($index.runs) } else { @() }
    while ($runs.Count -ge $MaxRuns) {
        $old = [string]$runs[0].path
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
        $runs = if ($runs.Count -eq 1) { @() } else { @($runs[1..($runs.Count - 1)]) }
    }
    $id = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss") + "-" + [guid]::NewGuid().ToString("N").Substring(0, 8)
    $dir = Join-Path (Get-RunsDir $Root) $id
    Ensure-Dir (Join-Path $dir "tickets")
    Write-JsonAtomic (Join-Path $dir "run.json") ([ordered]@{ id = $id; startedAt = Get-IsoNow; model = $Model; queue = @($Queue | ForEach-Object { $_.number }); status = "running"; path = $dir })
    $runs += ,[pscustomobject]@{ id = $id; startedAt = Get-IsoNow; path = $dir; model = $Model }
    Write-JsonAtomic $indexPath ([ordered]@{ max = $MaxRuns; updatedAt = Get-IsoNow; runs = $runs })
    return $dir
}

function Set-RunOutcome([string] $RunDir, [string] $Status, [hashtable] $Fields = @{}) {
    $path = Join-Path $RunDir "run.json"
    $meta = Read-JsonFile $path
    if (-not $meta) { $meta = [pscustomobject]@{} }
    $meta.status = $Status
    $meta.finishedAt = Get-IsoNow
    foreach ($key in $Fields.Keys) {
        if ($meta.PSObject.Properties.Name -contains $key) { $meta.$key = $Fields[$key] }
        else { $meta | Add-Member -NotePropertyName $key -NotePropertyValue $Fields[$key] }
    }
    Write-JsonAtomic $path $meta
}

function Invoke-Plan($Queue, [string] $Model, [bool] $Json) {
    if ($Json) { [Console]::Out.WriteLine(([ordered]@{ model = $Model; queue = $Queue } | ConvertTo-Json -Depth 8)); return }
    Write-Host "batch model: $Model"
    Write-Host "queue ($($Queue.Count)):"
    $i = 1
    foreach ($item in $Queue) { Write-Host ("  {0,2}. #{1} [{2}] {3}" -f $i, $item.number, $item.state, $item.title); $i++ }
}

function Invoke-Doctor([string] $Root, $Config) {
    $path = Get-OpenCodePath
    Write-Host "repoRoot: $Root"
    Write-Host "model: $($Config.model)"
    Write-Host "fallbackModel: $($Config.fallbackModel)"
    Write-Host "modelFormat: $(if (Test-ModelId $Config.model) { 'ok' } else { 'INVALID' })"
    if (-not (Test-ModelId $Config.model) -or -not (Test-ModelId $Config.fallbackModel)) { return 1 }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Host "gh: FAIL - GitHub CLI not found" -ForegroundColor Red; return 1 }
    $ghAuth = (& gh auth status 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { Write-Host "gh auth: FAIL - $($ghAuth.Trim())" -ForegroundColor Red; return 1 }
    Write-Host "gh auth: ok"
    $version = (& $path --version 2>&1 | Out-String).Trim()
    Write-Host "opencode: $version"
    $models = (& $path models 2>&1 | Out-String)
    if ($LASTEXITCODE -eq 0) {
        Write-Host ("primary listed: " + ($models -match [regex]::Escape([string]$Config.model)))
        Write-Host ("fallback listed: " + ($models -match [regex]::Escape([string]$Config.fallbackModel)))
    } else { Write-Host "models: unavailable (credentials/cache may be missing)" -ForegroundColor Yellow }
    $auth = (& $path auth list 2>&1 | Out-String)
    Write-Host "auth: $($auth.Trim())"
    Write-Host "doctor: PASS (no provider smoke request)" -ForegroundColor Green
    return 0
}

function Invoke-Status([string] $Root, [bool] $Json) {
    $path = Get-StatePath $Root
    if (-not (Test-Path -LiteralPath $path)) { if ($Json) { [Console]::Out.WriteLine("{}") } else { Write-Host "No state yet ($path)" }; return 0 }
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    if ($Json) { [Console]::Out.WriteLine($text) } else { Write-Host $text }
    return 0
}

function Invoke-Run([string] $Root, $Config, $Queue, [bool] $Once, [string] $ModelOverride, [Nullable[bool]] $StrictOverride) {
    if (-not $Queue -or $Queue.Count -eq 0) { Write-Host "Nothing to run."; return 0 }
    $openCode = Get-OpenCodePath
    $primary = if ($ModelOverride) { $ModelOverride } else { [string]$Config.model }
    if (-not (Test-ModelId $primary)) { throw "Invalid model id: $primary" }
    $strict = if ($null -ne $StrictOverride) { [bool]$StrictOverride } else { [bool]$Config.strictClose }
    $toRun = if ($Once) { @($Queue[0]) } else { @($Queue) }
    $runDir = New-Run -Root $Root -MaxRuns ([int]$Config.maxMachineLogRuns) -Model $primary -Queue $toRun
    $state = [ordered]@{ startedAt = Get-IsoNow; workspace = $Root; model = $primary; queue = @($toRun | ForEach-Object { $_.number }); completed = @(); failed = $null; paused = $null; current = $null; runDir = $runDir; finishedAt = $null }
    Write-JsonAtomic (Get-StatePath $Root) $state
    try {
        foreach ($item in $toRun) {
        $number = [int]$item.number
        $state.current = $number
        Write-JsonAtomic (Get-StatePath $Root) $state
        $ticketDir = Join-Path $runDir ("tickets\{0}" -f $number)
        Ensure-Dir $ticketDir
        Invoke-Hook ([string]$Config.hooks.beforeTicket) $Root $number "before"
        Write-Host "======== ticket #$number - $($item.title) ========" -ForegroundColor Green
        $task = Format-Prompt ([string]$Config.promptTemplate) $number ([string]$item.title)
        $result = Invoke-SupervisedTicket -OpenCodePath $openCode -Root $Root -Config $Config -PrimaryModel $primary -Issue $number -Title ([string]$item.title) -Task $task -TicketDir $ticketDir
        try { Invoke-Hook ([string]$Config.hooks.afterTicket) $Root $number "after" } catch { Write-Host "[warn] afterTicket hook: $_" -ForegroundColor Yellow }
        if ($result.status -eq "paused_user") {
            $state.paused = [ordered]@{ number = $number; reason = $result.reason; at = Get-IsoNow; ticketDir = $ticketDir }
            $state.finishedAt = Get-IsoNow
            Write-JsonAtomic (Get-StatePath $Root) $state
            Set-RunOutcome $runDir "paused_user" @{ pausedIssue = $number; reason = $result.reason }
            Write-Host "PAUSED awaiting human: #$number ($($result.reason))" -ForegroundColor Red
            return 3
        }
        if ($result.status -ne "finished" -or $result.exitCode -ne 0) {
            $state.failed = [ordered]@{ number = $number; reason = $result.reason; exitCode = $result.exitCode; at = Get-IsoNow; ticketDir = $ticketDir }
            $state.finishedAt = Get-IsoNow
            Write-JsonAtomic (Get-StatePath $Root) $state
            Set-RunOutcome $runDir "failed" @{ failedIssue = $number; reason = $result.reason }
            Write-Host "FAIL: #$number ($($result.reason))" -ForegroundColor Red
            return 2
        }
        $meta = Get-IssueMeta $number
        if ([string]$meta.state -ne "CLOSED" -and $strict) {
            $state.failed = [ordered]@{ number = $number; reason = "strict-close"; at = Get-IsoNow }
            $state.finishedAt = Get-IsoNow
            Write-JsonAtomic (Get-StatePath $Root) $state
            Set-RunOutcome $runDir "failed" @{ failedIssue = $number; reason = "strict-close" }
            Write-Host "FAIL: Issue #$number remains open." -ForegroundColor Red
            return 2
        }
        if ([string]$meta.state -ne "CLOSED") { Write-Host "WARN: Issue #$number remains open; continuing." -ForegroundColor Yellow }
        $state.completed = @($state.completed) + @($number)
        $state.current = $null
        Write-JsonAtomic (Get-StatePath $Root) $state
        }
    } catch {
        Set-RunOutcome $runDir "failed" @{ reason = "supervisor_exception"; error = "$_" }
        throw
    }
    $state.finishedAt = Get-IsoNow
    Write-JsonAtomic (Get-StatePath $Root) $state
    Set-RunOutcome $runDir "completed" @{ completed = $state.completed }
    Write-Host "Done. Completed: $($state.completed -join ', ')" -ForegroundColor Green
    return 0
}

function Main {
    $cmd = $Command.Trim().ToLowerInvariant()
    if (-not $cmd -or $cmd -in @("help", "-h", "--help")) { Show-Help; return 0 }
    $workspace = Get-FlagValue $Rest @("--workspace")
    $root = Find-RepoRoot $workspace
    if (-not $root) { Write-Host "Could not find repo root (.git)." -ForegroundColor Red; return 1 }
    $config = Load-Config $root (Get-FlagValue $Rest @("--config"))
    $json = Test-HasFlag $Rest @("--json")
    $parentRaw = Get-FlagValue $Rest @("--parent")
    $issuesRaw = Get-FlagValue $Rest @("--issues")
    $label = Get-FlagValue $Rest @("--label")
    $fromRaw = Get-FlagValue $Rest @("--from")
    $model = Get-FlagValue $Rest @("--model")
    $parent = $null
    if ($parentRaw) { $parent = [int]$parentRaw }
    elseif (-not $issuesRaw -and -not $label) { $parent = [int]$config.defaultParent }
    $issues = @()
    if (Test-FlagSpecified $Rest @("--issues")) {
        if ([string]::IsNullOrWhiteSpace($issuesRaw)) { throw "--issues requires at least one issue number." }
        $parts = @($issuesRaw -split '[,;\s]+' | Where-Object { $_ -ne "" })
        if ($parts.Count -eq 0 -or @($parts | Where-Object { $_ -notmatch '^\d+$' }).Count -gt 0) { throw "--issues must contain only comma-separated issue numbers." }
        $issues = @($parts | ForEach-Object { [int]$_ })
        $parent = $null
    }
    if ($parent) { $label = $null }
    if (-not $parent -and $issues.Count -eq 0 -and -not $label) { $label = [string]$config.readyLabel }
    try {
        switch ($cmd) {
            "doctor" { return Invoke-Doctor $root $config }
            "status" { return Invoke-Status $root $json }
            "plan" {
                $from = $null
                if ($fromRaw) { $from = [int]$fromRaw }
                $effectiveModel = if ($model) { $model } else { [string]$config.model }
                $queue = Resolve-Queue $parent $issues $label $from
                Invoke-Plan $queue $effectiveModel $json
                return 0
            }
            "run" {
                $from = $null
                if ($fromRaw) { $from = [int]$fromRaw }
                $effectiveModel = if ($model) { $model } else { [string]$config.model }
                $queue = Resolve-Queue $parent $issues $label $from
                if (Test-HasFlag $Rest @("--dry-run")) { Invoke-Plan $queue $effectiveModel $json; return 0 }
                $strict = $null
                if (Test-HasFlag $Rest @("--strict-close")) { $strict = $true }
                if (Test-HasFlag $Rest @("--continue-if-open")) { $strict = $false }
                return Invoke-Run $root $config $queue (Test-HasFlag $Rest @("--once")) $model $strict
            }
            default { Write-Host "Unknown command: $cmd" -ForegroundColor Red; Show-Help; return 1 }
        }
    } catch { Write-Host "ERROR: $_" -ForegroundColor Red; return 1 }
}

$script:ExitCode = Main
exit $script:ExitCode
