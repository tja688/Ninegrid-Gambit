# OpenCode Ticket Runner Reference

## Models

The canonical Models.dev identifiers used by this project are:

- Primary: `deepseek/deepseek-v4-flash`
- Fallback: `alibaba/qwen3.7-max`

Check the local installation with:

```powershell
opencode models
opencode auth list
```

The model names are configuration identifiers. `doctor` does not send a
smoke request and therefore does not prove that a provider token or endpoint
will accept a request.

## Queue precedence

`--issues` preserves the supplied order. `--parent` reads Markdown task-list
items such as `- [ ] #115`; if none are present it queries GitHub tracked
Issues. `--label` lists open Issues by number. Closed Issues are skipped.

## Process contract

Each worker process is launched with:

```text
opencode run --dir <repo> --agent ticket-implementer --model <model> --format json --auto <prompt>
```

The runner redirects stdout/stderr to files. It does not use pipe EOF as a
health signal and does not reuse sessions. The worker's final response must
contain one of:

```text
TICKET_STATUS: IMPLEMENTED
TICKET_STATUS: NEEDS_HUMAN
TICKET_STATUS: FAILED
```

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Queue completed |
| 1 | Usage, configuration, or environment error |
| 2 | A ticket failed |
| 3 | Queue paused for human intervention |

## Runtime files

```text
.opencode/ticket-runner/
  state.json
  runs/index.json
  runs/<run-id>/run.json
  runs/<run-id>/tickets/<issue>/
    events.ndjson
    stdout.log
    stderr.log
    meta.json
```
