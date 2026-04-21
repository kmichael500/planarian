#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Deploy Planarian dev directly from this machine.

Usage:
  scripts/deploy-dev.sh [--api-only|--web-only]

Environment overrides:
  AZURE_SUBSCRIPTION_ID   Azure subscription id
  AZURE_RESOURCE_GROUP    Azure resource group
  API_WEBAPP_NAME         Azure App Service name for the API
  STATIC_WEBAPP_NAME      Azure Static Web App name
  REACT_APP_SERVER_URL    API URL baked into the web build
USAGE
}

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date +%Y%m%d%H%M%S)"
TMP_BASE="${TMPDIR:-/tmp}/planarian-dev-deploy-${TIMESTAMP}"

AZURE_SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID:-66757d3a-24fe-47b8-85be-8063f5f1e36b}"
AZURE_RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-planarian}"
API_WEBAPP_NAME="${API_WEBAPP_NAME:-wa-planarian-dev}"
STATIC_WEBAPP_NAME="${STATIC_WEBAPP_NAME:-swa-planarian-dev}"
REACT_APP_SERVER_URL="${REACT_APP_SERVER_URL:-https://wa-planarian-dev.azurewebsites.net}"

DEPLOY_API=true
DEPLOY_WEB=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-only)
      DEPLOY_API=true
      DEPLOY_WEB=false
      shift
      ;;
    --web-only)
      DEPLOY_API=false
      DEPLOY_WEB=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

require_command az
require_command dotnet
require_command npm
require_command npx
require_command zip

echo "Using Azure subscription: ${AZURE_SUBSCRIPTION_ID}"
az account set --subscription "${AZURE_SUBSCRIPTION_ID}"

if [[ "${DEPLOY_API}" == true ]]; then
  API_PUBLISH_DIR="${TMP_BASE}/api-publish"
  API_ZIP="${TMP_BASE}/planarian-api-dev.zip"

  echo "Publishing API..."
  dotnet publish "${ROOT_DIR}/Planarian/Planarian/Planarian.csproj" \
    --configuration Release \
    --output "${API_PUBLISH_DIR}"

  echo "Packaging API..."
  mkdir -p "${TMP_BASE}"
  (
    cd "${API_PUBLISH_DIR}"
    zip -qr "${API_ZIP}" .
  )

  echo "Deploying API to ${API_WEBAPP_NAME}..."
  az webapp deploy \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${API_WEBAPP_NAME}" \
    --src-path "${API_ZIP}" \
    --type zip

  echo "Restarting API to clear in-memory upload state..."
  az webapp restart \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${API_WEBAPP_NAME}"
fi

if [[ "${DEPLOY_WEB}" == true ]]; then
  echo "Building web with REACT_APP_SERVER_URL=${REACT_APP_SERVER_URL}..."
  (
    cd "${ROOT_DIR}/Planarian.Web"
    REACT_APP_SERVER_URL="${REACT_APP_SERVER_URL}" npm run build
  )

  echo "Fetching Static Web App deployment token..."
  STATIC_WEBAPP_DEPLOYMENT_TOKEN="$(
    az staticwebapp secrets list \
      --name "${STATIC_WEBAPP_NAME}" \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --query properties.apiKey \
      -o tsv
  )"

  if [[ -z "${STATIC_WEBAPP_DEPLOYMENT_TOKEN}" ]]; then
    echo "Failed to fetch Static Web App deployment token." >&2
    exit 1
  fi

  echo "Deploying web to ${STATIC_WEBAPP_NAME}..."
  (
    cd /tmp
    npx -y @azure/static-web-apps-cli deploy "${ROOT_DIR}/Planarian.Web/build" \
      --deployment-token "${STATIC_WEBAPP_DEPLOYMENT_TOKEN}" \
      --env production
  )
fi

echo "Dev deployment complete."
