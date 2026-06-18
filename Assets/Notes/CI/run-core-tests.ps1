# Runs NineGrid.Core.Tests via Unity EditMode Test Runner (batchmode).
# Close the Unity Editor for this project before running.

param(
    [string]$UnityPath = $env:UNITY_PATH,
    [string]$ProjectPath = "",
    [string]$Assembly = $(if ($env:NINEGRID_TEST_ASSEMBLY) { $env:NINEGRID_TEST_ASSEMBLY } else { "NineGrid.Core.Tests" })
)

$ErrorActionPreference = "Stop"

if (-not $ProjectPath) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
}

$ArtifactsDir = Join-Path $PSScriptRoot "artifacts"
$ResultsFile = Join-Path $ArtifactsDir "ninegrid-core-editmode-results.xml"
$LogFile = Join-Path $ArtifactsDir "unity-test.log"

function Resolve-UnityEditorPath {
    param([string]$ProjectRoot)

    if ($UnityPath -and (Test-Path $UnityPath)) {
        return (Resolve-Path $UnityPath).Path
    }

    $versionFile = Join-Path $ProjectRoot "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "Cannot find ProjectVersion.txt under: $ProjectRoot"
    }

    $versionLine = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
    if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "Failed to parse m_EditorVersion from $versionFile"
    }
    $editorVersion = $Matches[1].Trim()

    $candidates = @(
        (Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Unity Editor $editorVersion not found. Set UNITY_PATH to Unity.exe."
}

try {
    $unityExe = Resolve-UnityEditorPath -ProjectRoot $ProjectPath
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

Write-Host "Project : $ProjectPath"
Write-Host "Unity   : $unityExe"
Write-Host "Assembly: $Assembly"
Write-Host "Results : $ResultsFile"

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", "editmode",
    "-assemblyNames", $Assembly,
    "-testResults", $ResultsFile,
    "-logFile", $LogFile
)

& $unityExe @arguments
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 1 }

if ($exitCode -eq 0) {
    if (Test-Path $ResultsFile) {
        $xml = [xml](Get-Content $ResultsFile)
        $root = $xml."test-run"
        if ($root) {
            $total = $root.total
            $passed = $root.passed
            $failed = $root.failed
            $skipped = $root.skipped
            Write-Host "Tests: total=$total passed=$passed failed=$failed skipped=$skipped"
            if ([int]$failed -gt 0) {
                exit 2
            }
        }
    }
    Write-Host "NineGrid Core tests passed."
    exit 0
}

if ($exitCode -eq 2) {
    Write-Error "Unity reported test failures. See $LogFile and $ResultsFile"
    exit 2
}

Write-Error "Unity batchmode failed (exit $exitCode). See $LogFile"
exit $exitCode
