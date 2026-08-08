[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\attribute-room\prompt.txt'
opencode run --continue -s 'ses_0210ffc72ffemM1I936ts1Tuky' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\attribute-room\agent.log'
exit $LASTEXITCODE
