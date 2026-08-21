#!/usr/bin/env bash
# Cloud Agent "unity-editor" terminal for Ninegrid Gambit.
#
# Keeps a persistent headless Editor open on the project so the agent can drive
# it with the Unity CLI (`unity status`, `unity command recompile`, `unity command
# console`, `unity command run_tests`, ...) exactly as the repo's unity-cli rules
# describe. Runs in the foreground so its log stays visible and restartable.
set -uo pipefail

export PATH="${HOME}/.local/bin:${PATH}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' "${REPO_ROOT}/ProjectSettings/ProjectVersion.txt" | tr -d '[:space:]')"
UNITY_DISPLAY="${UNITY_DISPLAY:-:99}"
EDITOR_BIN="${HOME}/Unity/Hub/Editor/${UNITY_VERSION}/Editor/Unity"

# Block until a license is active — the Editor cannot open the project otherwise.
echo "==> Waiting for an active Unity license (set the service-account secrets if this hangs)"
until unity license status --format json 2>/dev/null | grep -q '"active": true'; do
  sleep 5
done

echo "==> Opening ${REPO_ROOT} in Unity ${UNITY_VERSION} (-automated) on ${UNITY_DISPLAY}"
exec env DISPLAY="${UNITY_DISPLAY}" "${EDITOR_BIN}" \
  -projectPath "${REPO_ROOT}" \
  -automated \
  -logFile -
