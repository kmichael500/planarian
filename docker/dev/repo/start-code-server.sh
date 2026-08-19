#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" == "0" ]]; then
  mkdir -p \
    /home/coder/.local/share/code-server \
    /home/coder/.nuget/packages \
    /home/coder/.npm \
    /workspace/Planarian.Web/node_modules
  chown coder:coder \
    /home/coder/.local/share/code-server \
    /home/coder/.nuget/packages \
    /home/coder/.npm \
    /workspace/Planarian.Web/node_modules
  exec runuser -u coder --preserve-environment -- env HOME=/home/coder "$0" "$@"
fi

exec code-server "$@"
