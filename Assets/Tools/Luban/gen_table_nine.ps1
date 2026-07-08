param(
    [string]$LubanExe = $env:LUBAN_EXE,
    [switch]$BootstrapVisual
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$defaultLubanExe = "C:\Users\jinji\Desktop\应用\Luban\Luban.exe"
if ([string]::IsNullOrWhiteSpace($LubanExe)) {
    $LubanExe = $defaultLubanExe
}

if (-not (Test-Path -LiteralPath $LubanExe)) {
    throw "Luban executable was not found. Set LUBAN_EXE or pass -LubanExe. Tried: $LubanExe"
}

if ($BootstrapVisual) {
    $venvPython = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
    $bootstrapScript = Join-Path $PSScriptRoot "bootstrap_content_visual.py"
    $bootstrapVisualScript = Join-Path $PSScriptRoot "bootstrap_visual_tables.py"
    if (Test-Path -LiteralPath $venvPython) {
        & $venvPython $bootstrapScript
        & $venvPython $bootstrapVisualScript
    }
    else {
        python $bootstrapScript
        python $bootstrapVisualScript
    }
    if ($LASTEXITCODE -ne 0) {
        throw "content visual bootstrap failed with exit code $LASTEXITCODE"
    }
}

$conf = Join-Path $PSScriptRoot "luban.conf"
$codeDir = Join-Path $projectRoot "Assets\Scripts\NineGrid.Foundation\NineGrid.Content\Generated\Luban"
$dataDir = Join-Path $projectRoot "Assets\StreamingAssets\TableNine\LubanData"

& $LubanExe `
    -t client `
    -c cs-simple-json `
    -d json `
    --conf $conf `
    --timeZone "Asia/Shanghai" `
    -x outputCodeDir=$codeDir `
    -x outputDataDir=$dataDir `
    -x pathValidator.rootDir=$projectRoot `
    -x json.compact=0

if ($LASTEXITCODE -ne 0) {
    throw "Luban generation failed with exit code $LASTEXITCODE"
}
