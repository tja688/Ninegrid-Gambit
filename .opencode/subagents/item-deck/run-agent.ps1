[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\item-deck\prompt.txt'
opencode run --title 'subagent:item-deck' -m 'opencode-go/deepseek-v4-flash' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\item-deck\agent.log'
exit $LASTEXITCODE
