# 临时分析脚本 2：registrylog 结构 + corelog slime 局定位
$reg = "Assets/Notes/Logs/OtherLog/RegistryLog/registrylog-20260812-181031-seed4773169723558279157.json"
$core = "Assets/Notes/Logs/CoreLog/corelog-20260812-181031-seed4773169723558279157.json"

$rj = Get-Content $reg -Raw | ConvertFrom-Json
Write-Output ("registry top-level: " + ($rj.PSObject.Properties.Name -join ", "))
$rev = $rj.events
if (-not $rev) { $rev = $rj.entries }
if (-not $rev) { $rev = $rj.ops }
if ($rev) {
    Write-Output ("registry count: " + $rev.Count)
    $rev[0] | ConvertTo-Json -Depth 5 -Compress
    # uid -> defId 映射（找 slime 和 armor totem）
    $hits = @($rev | Where-Object { ($_ | ConvertTo-Json -Depth 5 -Compress) -match "slime|armor_totem" })
    Write-Output ("slime/totem hits: " + $hits.Count)
    $hits | Select-Object -First 40 | ForEach-Object { $_ | ConvertTo-Json -Depth 5 -Compress }
}
