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

exec npm start
