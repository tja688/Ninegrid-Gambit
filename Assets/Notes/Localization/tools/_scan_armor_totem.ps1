# 临时分析脚本 7：各 session 战斗中怪物身上出现过的最大护甲
Get-ChildItem "Assets/Notes/Logs/OtherLog/BattleLog" -Filter "battlelog-*.json" | Sort-Object Name | ForEach-Object {
    $bj = Get-Content $_.FullName -Raw | ConvertFrom-Json
    $max = 0; $hits = 0
    foreach ($op in $bj.ops) {
        if (-not $op.events) { continue }
        foreach ($e in $op.events) {
            if ($e.type -eq "ArmorChanged" -and $e.delta -lt 0 -and $e.cardUid -ne 1) {
                $before = $e.remainingArmor - $e.delta
                if ($before -gt $max) { $max = $before }
                $hits++
            }
        }
    }
    Write-Output ($_.Name + "  maxMonsterArmorSeen=" + $max + " (negArmorEvents=" + $hits + ")")
}
