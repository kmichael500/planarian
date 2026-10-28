#!/usr/bin/env bash
set -euo pipefail

cd /workspace/Planarian.Web

if [[ "$(id -u)" == "0" ]]; then
  mkdir -p node_modules /home/agent/.npm
  chown agent:agent node_modules /home/agent/.npm
  exec runuser -u agent --preserve-environment -- env HOME=/home/agent "$0"
fi

lock_hash="$(sha256sum package-lock.json | awk '{print $1}')"
marker="node_modules/.planarian-package-lock"
installed_hash=""
if [[ -f "$marker" ]]; then
  installed_hash="$(cat "$marker")"
fi

if [[ "$installed_hash" != "$lock_hash" ]]; then
  echo "Installing frontend dependencies for the container..."
  npm ci
  printf '%s' "$lock_hash" > "$marker"
fi

if [[ "${PLANARIAN_FRONTEND_MODE:-development}" == "production" ]]; then
  if [[ "${PLANARIAN_FRONTEND_PREBUILT:-0}" != "1" ]]; then
    echo "Building production frontend for browser E2E validation..."
    npm run build
  elif [[ ! -f /workspace/Planarian.Web/build/index.html ]]; then
    echo "PLANARIAN_FRONTEND_PREBUILT=1 but build/index.html is missing." >&2
    exit 2
  fi
  exec node /workspace/docker/dev/serve-static-build.mjs /workspace/Planarian.Web/build
fi

exec npm start
