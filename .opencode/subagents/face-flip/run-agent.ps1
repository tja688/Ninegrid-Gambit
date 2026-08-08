[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\face-flip\prompt.txt'
opencode run --title 'subagent:face-flip' -m 'opencode-go/deepseek-v4-flash' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\face-flip\agent.log'
exit $LASTEXITCODE
