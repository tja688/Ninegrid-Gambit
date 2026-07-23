---
name: unity-automated-launch
description: >-
  Launch any Unity project with -automated (direct Editor start, no Hub IPC) so
  Unity CLI / com.unity.pipeline is not blocked by modal dialogs after external
  .unity edits. Use when starting or restarting the Editor for agent work, when
  Pipeline times out on dialogs, or when the user asks for -automated / 无 Hub
  启动 Unity. Portable: copy this skill folder into another repo's .cursor/skills/.
---

# Unity `-automated` launch (portable)

Agent Editor sessions should use **direct `Unity.exe` + `-automated`**. Hub “Open”
does not pass that flag; external `.unity` edits then show a modal that blocks the
Pipeline main thread (same failure mode as MCP).

Spelling is **`-automated`**, not `-automate`.

## Transplant

Copy the whole folder to another project:

```text
.cursor/skills/unity-automated-launch/
  SKILL.md
  scripts/Start-UnityAutomated.ps1
```

Requirements on the target machine/project:

1. `unity` CLI on PATH
2. Matching Editor installed (`ProjectSettings/ProjectVersion.txt`)
3. For Pipeline wait/CLI control: `com.unity.pipeline` in the project (`unity pipeline install`)

## When to use

- Editor not open, or unclear whether it has `-automated`
- `unity command` / Pipeline timeouts that look like a modal dialog
- User asks for no-Hub / automated launch

If an instance is already running **without** `-automated`: quit it, then relaunch
with this skill (flags cannot be added to a live process).

## Launch (prefer the script)

From the **Unity project root** (directory that contains `Assets/` + `ProjectSettings/`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/unity-automated-launch/scripts/Start-UnityAutomated.ps1"
```

Or pass an explicit root (any cwd):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "path/to/unity-automated-launch/scripts/Start-UnityAutomated.ps1" -ProjectPath "D:\MyUnityProject"
```

Switches:

| Switch | Effect |
|--------|--------|
| `-SkipIfRunning` | Exit 0 if Pipeline already reachable for this project (does **not** check `-automated`) |
| `-NoWait` | Start process only; do not poll Pipeline |
| `-TimeoutSec N` | Pipeline wait limit (default 180) |

The script: reads `ProjectSettings/ProjectVersion.txt` → resolves `Unity.exe` via
`unity editors -i` → starts with `-projectpath` + `-automated` (PS7 `ArgumentList`,
or quoted `Arguments` on Windows PowerShell 5.1) → polls `unity pipeline list`
until reachable.

## Manual equivalent

```powershell
$proj = "D:\MyUnityProject"   # absolute project root
$ver  = (Select-String -Path "$proj\ProjectSettings\ProjectVersion.txt" -Pattern 'm_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
$editors = unity editors -i --format json | ConvertFrom-Json
$exe = ($editors.data | Where-Object { $_.version -eq $ver } | Select-Object -First 1).location
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.UseShellExecute = $false
if ($null -ne $psi.ArgumentList) {
  $psi.ArgumentList.Add('-projectpath'); $psi.ArgumentList.Add($proj); $psi.ArgumentList.Add('-automated')
} else {
  $psi.Arguments = "-projectpath `"$proj`" -automated"   # PS 5.1 / .NET Framework
}
[Diagnostics.Process]::Start($psi) | Out-Null
# Poll: unity pipeline list --format json until this project's isReachable=true
```

Do **not** pass a space-containing `-projectpath` via a fragile `Start-Process -ArgumentList` join that splits the path.

**Note:** `unity pipeline list` can report `isRunning: true` with stale Hub/licensing leftovers even when no Editor process exists. Trust `pipelineServer.isReachable` (and/or a live `Unity.exe` process), not `isRunning` alone.

## After launch

```powershell
unity pipeline list --format json
unity command eval "var a=System.Environment.GetCommandLineArgs(); return string.Join(' ', a);" --project-path "<project-root>" --format json
```

Confirm args contain `-automated` and Pipeline `isReachable=true`, then use `unity command …`.

## Boundaries

| Topic | Reality |
|-------|---------|
| Prevent dialog stall | With `-automated`, external scene change + Refresh usually does not block the main thread |
| Dialog already up | CLI cannot click it; quit Editor and relaunch with this skill |
| Hub processes | May still appear for licensing; Editor itself should not need `-useHub` |
| No Pipeline package | Editor can still start with `-NoWait`; install `com.unity.pipeline` before relying on `unity command` |
| Safe Mode | **Forbidden for this project.** Do not Enter Safe Mode. After launch, verify `EditorUtility.isInSafeMode` is false; if true, quit, fix compile errors, relaunch. See `.cursor/rules/unity-cli.mdc`. |
