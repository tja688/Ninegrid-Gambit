#!/usr/bin/env bash
# Cloud Agent environment "install" phase for Ninegrid Gambit.
#
# Durable, idempotent provisioning that is baked into the environment snapshot:
#   1. System libraries the Unity Editor needs to run headlessly on Ubuntu 24.04.
#   2. The official Unity CLI (`unity`).
#   3. The exact Unity Editor version the project pins (ProjectVersion.txt).
#
# It intentionally does NOT sign in, activate a license, start a display server,
# or open the project — those need per-boot state / secrets and live in start.sh.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' "${REPO_ROOT}/ProjectSettings/ProjectVersion.txt" | tr -d '[:space:]')"
export PATH="${HOME}/.local/bin:${PATH}"

echo "==> Ninegrid Gambit install: Unity ${UNITY_VERSION}"

# 1. System libraries for a headless Unity Editor. Guarded so re-runs are cheap.
#    libnss3 keeps its name on Ubuntu 24.04 (unlike the t64-renamed packages),
#    so it is a reliable sentinel for "this apt line already ran".
if ! dpkg -s libnss3 >/dev/null 2>&1; then
  echo "==> Installing headless Unity runtime libraries"
  export DEBIAN_FRONTEND=noninteractive
  sudo apt-get update -qq
  sudo apt-get install -y --no-install-recommends \
    libgtk-3-0 libnss3 libnspr4 libasound2t64 libgbm1 libxss1 libxtst6 \
    libxrandr2 libgl1 libglu1-mesa libx11-6 libxcursor1 libxi6 libxinerama1 \
    libxrender1 libglib2.0-0t64 libdbus-1-3 libcurl4t64 xvfb
fi

# 2. Unity CLI (single self-contained binary, installs to ~/.local/bin).
if ! command -v unity >/dev/null 2>&1; then
  echo "==> Installing the Unity CLI"
  curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
fi
export PATH="${HOME}/.local/bin:${PATH}"
unity --version

# 3. The project's Editor. `unity install` is a large (~8 GB installed) download,
#    so only run it when that exact version is not already present.
if unity editors --installed --format json | grep -q "\"${UNITY_VERSION}\""; then
  echo "==> Unity Editor ${UNITY_VERSION} already installed"
else
  echo "==> Installing Unity Editor ${UNITY_VERSION} (this is large)"
  unity install "${UNITY_VERSION}" --yes --accept-eula
fi

echo "==> install phase complete"
