#!/usr/bin/env bash
set -euo pipefail

cd /workspace
project="Planarian/Planarian/Planarian.csproj"

if [[ "${PLANARIAN_DATASET:-generic}" == "e2e" ]]; then
  # Browser E2E owns a disposable runtime and does not need hot reload. Keep
  # its generated MSBuild artifacts out of the bind-mounted source tree so
  # IDE/build activity on the host cannot race the container compiler.
  exec dotnet run \
    --project "$project" \
    --artifacts-path /tmp/planarian-e2e-artifacts \
    --no-launch-profile
fi

exec dotnet watch \
  --project "$project" \
  run --no-launch-profile --urls http://0.0.0.0:8080
