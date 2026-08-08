[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\trap-effects\prompt.txt'
opencode run --continue -s 'ses_021105de1ffeCaNc0UbGrp0Zrd' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\trap-effects\agent.log'
exit $LASTEXITCODE
