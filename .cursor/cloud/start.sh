#!/usr/bin/env bash
# Cloud Agent environment "start" phase for Ninegrid Gambit.
#
# Per-boot reconciliation that must tolerate restarts and then return:
#   1. Ensure a virtual X display (Unity runs with -automated / a GUI context).
#   2. Sign in to Unity non-interactively with a Cloud service account.
#   3. Activate the Unity Editor license.
#
# Requires the secrets UNITY_SERVICE_ACCOUNT_ID and UNITY_SERVICE_ACCOUNT_SECRET
# (added in the Cloud Agent Secrets panel). Without them the Editor cannot run.
set -uo pipefail

export PATH="${HOME}/.local/bin:${PATH}"
UNITY_DISPLAY="${UNITY_DISPLAY:-:99}"

# 1. Virtual display for the Editor. Idempotent: skip if one already answers.
if ! DISPLAY="${UNITY_DISPLAY}" xdpyinfo >/dev/null 2>&1; then
  echo "==> Starting Xvfb on ${UNITY_DISPLAY}"
  Xvfb "${UNITY_DISPLAY}" -screen 0 1920x1080x24 -nolisten tcp >/tmp/xvfb.log 2>&1 &
  for _ in $(seq 1 20); do
    DISPLAY="${UNITY_DISPLAY}" xdpyinfo >/dev/null 2>&1 && break
    sleep 0.5
  done
fi

# 2 + 3. Authenticate and activate the license. Guarded so a warm reboot that
#        already holds an active seat does not re-activate.
if unity license status --format json 2>/dev/null | grep -q '"active": true'; then
  echo "==> Unity license already active"
  exit 0
fi

if [[ -z "${UNITY_SERVICE_ACCOUNT_ID:-}" || -z "${UNITY_SERVICE_ACCOUNT_SECRET:-}" ]]; then
  echo "!!  UNITY_SERVICE_ACCOUNT_ID / UNITY_SERVICE_ACCOUNT_SECRET are not set."
  echo "!!  Add them in the Secrets panel so the Unity Editor license can activate."
  echo "!!  The Editor and CLI are installed; they just cannot run without a license."
  exit 0
fi

echo "==> Signing in to Unity with the service account"
unity auth login --client-id "${UNITY_SERVICE_ACCOUNT_ID}" \
  --secret-from-stdin <<<"${UNITY_SERVICE_ACCOUNT_SECRET}"

echo "==> Activating the Unity license"
unity license activate

unity license status --format json
echo "==> start phase complete"
