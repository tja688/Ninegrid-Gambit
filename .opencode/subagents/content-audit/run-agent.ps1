[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\content-audit\prompt.txt'
opencode run --title 'subagent:content-audit' -m 'opencode-go/deepseek-v4-flash' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\content-audit\agent.log'
exit $LASTEXITCODE
