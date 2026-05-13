#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Deploy Planarian directly from this machine.

Usage:
  scripts/deploy.sh [--environment dev|production] [--api-only|--web-only]
  scripts/deploy.sh [--dev|--prod] [--api-only|--web-only]

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

ENVIRONMENT="dev"
DEPLOY_API=true
DEPLOY_WEB=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --environment)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for --environment" >&2
        usage >&2
        exit 1
      fi
      ENVIRONMENT="$2"
      shift 2
      ;;
    --dev)
      ENVIRONMENT="dev"
      shift
      ;;
    --prod|--production)
      ENVIRONMENT="production"
      shift
      ;;
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

case "${ENVIRONMENT}" in
  dev)
    DEPLOY_LABEL="dev"
    DEFAULT_AZURE_SUBSCRIPTION_ID="66757d3a-24fe-47b8-85be-8063f5f1e36b"
    DEFAULT_AZURE_RESOURCE_GROUP="rg-planarian"
    DEFAULT_API_WEBAPP_NAME="wa-planarian-dev"
    DEFAULT_STATIC_WEBAPP_NAME="swa-planarian-dev"
    DEFAULT_REACT_APP_SERVER_URL="https://wa-planarian-dev.azurewebsites.net"
    ;;
  prod|production)
    ENVIRONMENT="production"
    DEPLOY_LABEL="production"
    DEFAULT_AZURE_SUBSCRIPTION_ID="66757d3a-24fe-47b8-85be-8063f5f1e36b"
    DEFAULT_AZURE_RESOURCE_GROUP="rg-planarian"
    DEFAULT_API_WEBAPP_NAME="wa-planarian"
    DEFAULT_STATIC_WEBAPP_NAME="swa-planarian"
    DEFAULT_REACT_APP_SERVER_URL="https://wa-planarian.azurewebsites.net"
    ;;
  *)
    echo "Unsupported environment: ${ENVIRONMENT}" >&2
    usage >&2
    exit 1
    ;;
esac

TMP_BASE="${TMPDIR:-/tmp}/planarian-${DEPLOY_LABEL}-deploy-${TIMESTAMP}"

AZURE_SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID:-${DEFAULT_AZURE_SUBSCRIPTION_ID}}"
AZURE_RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-${DEFAULT_AZURE_RESOURCE_GROUP}}"
API_WEBAPP_NAME="${API_WEBAPP_NAME:-${DEFAULT_API_WEBAPP_NAME}}"
STATIC_WEBAPP_NAME="${STATIC_WEBAPP_NAME:-${DEFAULT_STATIC_WEBAPP_NAME}}"
REACT_APP_SERVER_URL="${REACT_APP_SERVER_URL:-${DEFAULT_REACT_APP_SERVER_URL}}"

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

echo "Deploy environment: ${ENVIRONMENT}"
echo "Using Azure subscription: ${AZURE_SUBSCRIPTION_ID}"
az account set --subscription "${AZURE_SUBSCRIPTION_ID}"

if [[ "${DEPLOY_API}" == true ]]; then
  API_PUBLISH_DIR="${TMP_BASE}/api-publish"
  API_ZIP="${TMP_BASE}/planarian-api-${DEPLOY_LABEL}.zip"

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

echo "${DEPLOY_LABEL^} deployment complete."
