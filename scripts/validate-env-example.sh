#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# This is the exact non-evaluating dotenv loader used by deploy-dev.sh.
# shellcheck source=load-dotenv.sh
source "${ROOT_DIR}/scripts/load-dotenv.sh"
load_dotenv "${ROOT_DIR}/Planarian.Web/.env.example"

node -e '
const value = process.env.REACT_APP_API_ORIGIN_MAPPINGS;
if (!value) throw new Error("REACT_APP_API_ORIGIN_MAPPINGS was not loaded");
const mappings = JSON.parse(value);
if (Object.getPrototypeOf(mappings) !== Object.prototype) throw new Error("Expected a JSON object");
console.log(".env.example API origin mappings are valid JSON:", mappings);
'
