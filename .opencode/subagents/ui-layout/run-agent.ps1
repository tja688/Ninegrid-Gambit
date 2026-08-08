[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\ui-layout\prompt.txt'
opencode run --title 'subagent:ui-layout' -m 'opencode-go/deepseek-v4-flash' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\ui-layout\agent.log'
exit $LASTEXITCODE
