[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:SPAWNMSG = Get-Content -Raw -LiteralPath 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\Refill-LogForensics\prompt.txt'
opencode run -m 'opencode-go/deepseek-v4-flash' $env:SPAWNMSG *> 'C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\.opencode\subagents\Refill-LogForensics\agent.log'
exit $LASTEXITCODE
