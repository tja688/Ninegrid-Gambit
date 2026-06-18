#!/usr/bin/env bash
# Runs NineGrid.Core.Tests via Unity EditMode Test Runner (batchmode).
# Close the Unity Editor for this project before running.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$(cd "$SCRIPT_DIR/../../.." && pwd)"
ARTIFACTS_DIR="$SCRIPT_DIR/artifacts"
RESULTS_FILE="$ARTIFACTS_DIR/ninegrid-core-editmode-results.xml"
LOG_FILE="$ARTIFACTS_DIR/unity-test.log"
ASSEMBLY="${NINEGRID_TEST_ASSEMBLY:-NineGrid.Core.Tests}"

resolve_unity_path() {
  if [[ -n "${UNITY_PATH:-}" && -x "$UNITY_PATH" ]]; then
    echo "$UNITY_PATH"
    return 0
  fi

  local version_file="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"
  if [[ ! -f "$version_file" ]]; then
    echo "Cannot find ProjectVersion.txt under: $PROJECT_PATH" >&2
    return 1
  fi

  local editor_version
  editor_version="$(grep -E '^m_EditorVersion:' "$version_file" | head -n1 | sed 's/^m_EditorVersion:[[:space:]]*//')"
  if [[ -z "$editor_version" ]]; then
    echo "Failed to parse m_EditorVersion from $version_file" >&2
    return 1
  fi

  local candidates=(
    "/Applications/Unity/Hub/Editor/${editor_version}/Unity.app/Contents/MacOS/Unity"
    "/opt/unity/Editor/Unity"
    "$HOME/Unity/Hub/Editor/${editor_version}/Editor/Unity"
  )

  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]]; then
      echo "$candidate"
      return 0
    fi
  done

  echo "Unity Editor ${editor_version} not found. Set UNITY_PATH." >&2
  return 1
}

UNITY_BIN="$(resolve_unity_path)" || exit 1

GUARD_SCRIPT="$SCRIPT_DIR/check-core-guards.sh"
if [[ -x "$GUARD_SCRIPT" || -f "$GUARD_SCRIPT" ]]; then
  echo "Running Core architecture guard..."
  bash "$GUARD_SCRIPT" "$PROJECT_PATH"
fi

mkdir -p "$ARTIFACTS_DIR"

echo "Project : $PROJECT_PATH"
echo "Unity   : $UNITY_BIN"
echo "Assembly: $ASSEMBLY"
echo "Results : $RESULTS_FILE"

set +e
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$PROJECT_PATH" \
  -runTests \
  -testPlatform editmode \
  -assemblyNames "$ASSEMBLY" \
  -testResults "$RESULTS_FILE" \
  -logFile "$LOG_FILE"
exit_code=$?
set -e

if [[ $exit_code -eq 0 ]]; then
  if [[ -f "$RESULTS_FILE" ]] && command -v xmllint >/dev/null 2>&1; then
    failed="$(xmllint --xpath 'string(/test-run/@failed)' "$RESULTS_FILE" 2>/dev/null || echo "")"
    total="$(xmllint --xpath 'string(/test-run/@total)' "$RESULTS_FILE" 2>/dev/null || echo "")"
    passed="$(xmllint --xpath 'string(/test-run/@passed)' "$RESULTS_FILE" 2>/dev/null || echo "")"
    if [[ -n "$failed" ]]; then
      echo "Tests: total=$total passed=$passed failed=$failed"
      if [[ "$failed" != "0" ]]; then
        exit 2
      fi
    fi
  fi
  echo "NineGrid Core tests passed."
  exit 0
fi

if [[ $exit_code -eq 2 ]]; then
  echo "Unity reported test failures. See $LOG_FILE and $RESULTS_FILE" >&2
  exit 2
fi

echo "Unity batchmode failed (exit $exit_code). See $LOG_FILE" >&2
exit "$exit_code"
