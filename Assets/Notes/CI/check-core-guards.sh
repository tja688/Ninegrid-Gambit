#!/usr/bin/env bash
# Architecture guardrails for NineGrid.Core (P3/P4).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="${1:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
CORE_DIR="$PROJECT_PATH/Assets/Scripts/NineGrid.Core"
SYSTEMS_DIR="$CORE_DIR/Systems"
ASMDEF_PATH="$CORE_DIR/NineGrid.Core.asmdef"

if ! command -v rg >/dev/null 2>&1; then
  echo "ripgrep (rg) not found on PATH." >&2
  exit 1
fi

if [[ ! -d "$CORE_DIR" ]]; then
  echo "Core directory not found: $CORE_DIR" >&2
  exit 1
fi

failures=()

if ! grep -Eq '"noEngineReferences"[[:space:]]*:[[:space:]]*true' "$ASMDEF_PATH"; then
  failures+=("NineGrid.Core.asmdef must set \"noEngineReferences\": true")
fi

while IFS= read -r line; do
  [[ -n "$line" ]] && failures+=("UnityEngine reference in Core: $line")
done < <(rg -n --pcre2 'using\s+UnityEngine\b|global::UnityEngine\b|(?<![A-Za-z0-9_])UnityEngine\.' "$CORE_DIR" --glob '*.cs' || true)

bypass_pattern='\.(PlaceCard|RemoveCard|ClearSlot|ClearBoardCards|SetAvatar|SetBlessed|AddToDrawPile|AddToPlayerCardPool|AddToEnemyCardPool|AddToItemSlots|RemoveUid|ReorderDrawPile|AddCoins|AddInteractionCount|AddRelic|AddSkill|SetPhase|MoveCard)\(|\.Stats\.SetBase\(|\.Counters\.(Set|Add)\(|\.(Zone|Slot)\.Value\s*=|registry\.(Create|Remove|MoveCard)\('

while IFS= read -r line; do
  [[ -n "$line" ]] && failures+=("System bypasses Action pipeline: $line")
done < <(rg -n --pcre2 "$bypass_pattern" "$SYSTEMS_DIR" \
  --glob '*.cs' \
  --glob '!ActionPipelineSystem.cs' \
  --glob '!TriggerSystem.cs' \
  --glob '!StatSystem.cs' || true)

clear_pattern='(deck|board|registry|player|run)\.(Clear|Reset)\('

while IFS= read -r line; do
  [[ -n "$line" ]] && failures+=("System clears/resets Model outside Action: $line")
done < <(rg -n --pcre2 "$clear_pattern" "$SYSTEMS_DIR" \
  --glob '*.cs' \
  --glob '!ActionPipelineSystem.cs' \
  --glob '!TriggerSystem.cs' \
  --glob '!StatSystem.cs' || true)

if ((${#failures[@]} > 0)); then
  echo "Core architecture guard FAILED (${#failures[@]} issue(s)):" >&2
  for item in "${failures[@]}"; do
    echo "  - $item" >&2
  done
  exit 1
fi

echo "Core architecture guard passed."
echo "  Core dir : $CORE_DIR"
echo "  Checks   : noEngineReferences, no UnityEngine, Systems -> Action pipeline"
exit 0
