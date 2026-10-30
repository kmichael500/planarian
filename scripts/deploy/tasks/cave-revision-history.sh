#!/usr/bin/env bash
# One-time Cave revision-history rollout. The permanent deployment workflow
# invokes registered tasks through generic lifecycle phases.
set -euo pipefail

phase="${1:?release task phase is required}"
migration_path="Planarian/Planarian.Migrations/Migrations/20261029194500_CaveRevisionsCompletion.cs"

require_operation() {
    : "${ConnectionStrings__default:?Set the scoped database connection string}"
    : "${RELEASE_OPERATIONS_PUBLISH_DIR:?Set the operations publish artifact directory}"
}

case "$phase" in
    preflight)
        require_operation
        : "${AZURE_RESOURCE_GROUP:?Set the Azure resource group}"
        : "${AZURE_WEBAPP_NAME:?Set the Azure App Service name}"
        [[ -f "$migration_path" ]] || {
            echo "Cave revision-history rollout requires migration: $migration_path" >&2
            exit 1
        }
        ;;

    before-migrations)
        require_operation
        : "${AZURE_RESOURCE_GROUP:?Set the Azure resource group}"
        : "${AZURE_WEBAPP_NAME:?Set the Azure App Service name}"
        : "${RESUME_MAINTENANCE:=false}"

        app_state="$(az webapp show \
            --resource-group "$AZURE_RESOURCE_GROUP" \
            --name "$AZURE_WEBAPP_NAME" \
            --query state --output tsv)"
        should_stop=true
        if [[ "$app_state" == Stopped && "$RESUME_MAINTENANCE" == true ]]; then
            should_stop=false
            echo "Resuming an explicitly acknowledged Cave revision-history rollout with the API already stopped."
        elif [[ "$app_state" != Running ]]; then
            echo "Cave revision-history rollout requires a running API before quiescence, or resume_maintenance=true when it is already stopped (current: $app_state)." >&2
            exit 1
        fi

        stop_scm="$(az webapp show \
            --resource-group "$AZURE_RESOURCE_GROUP" \
            --name "$AZURE_WEBAPP_NAME" \
            --query scmSiteAlsoStopped --output tsv)"
        disable_scm_separation="$(az webapp config appsettings list \
            --resource-group "$AZURE_RESOURCE_GROUP" \
            --name "$AZURE_WEBAPP_NAME" \
            --query "[?name=='WEBSITE_DISABLE_SCM_SEPARATION'].value | [0]" \
            --output tsv)"
        if [[ "$stop_scm" == [Tt][Rr][Uu][Ee] || "$disable_scm_separation" == [Tt][Rr][Uu][Ee] ]]; then
            echo "SCM/Kudu must remain separate and running while the main App Service is stopped." >&2
            exit 1
        fi

        if [[ "$should_stop" == true ]]; then
            az webapp stop \
                --resource-group "$AZURE_RESOURCE_GROUP" \
                --name "$AZURE_WEBAPP_NAME"
        fi
        ;;

    run)
        require_operation
        operations_dll="$RELEASE_OPERATIONS_PUBLISH_DIR/Planarian.Operations.dll"
        [[ -f "$operations_dll" ]] || {
            echo "Planarian operations artifact is missing: $operations_dll" >&2
            exit 1
        }
        dotnet "$operations_dll" initialize-cave-revision-history
        ;;

    after-api-deploy)
        : "${AZURE_RESOURCE_GROUP:?Set the Azure resource group}"
        : "${AZURE_WEBAPP_NAME:?Set the Azure App Service name}"
        az webapp start \
            --resource-group "$AZURE_RESOURCE_GROUP" \
            --name "$AZURE_WEBAPP_NAME"
        ;;

    *)
        echo "Unknown release task phase: $phase" >&2
        exit 2
        ;;
esac
