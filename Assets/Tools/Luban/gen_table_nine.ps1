param(
    [string]$LubanExe = $env:LUBAN_EXE
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

$conf = Join-Path $PSScriptRoot "luban.conf"
$codeDir = Join-Path $projectRoot "Assets\Scripts\NineGrid.Content\Generated\Luban"
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
