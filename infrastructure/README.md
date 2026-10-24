# Azure PostgreSQL configuration

This template manages **only** the extension allow-list of the existing `db-planarian` PostgreSQL flexible server. It does not redeploy the server, database, networking or any other Azure resource.

`azure.extensions` is an entire allow-list, not an append operation. The intended value is `POSTGIS,PG_TRGM`. Reconcile any newly allowed extensions with the template before deployment.

## Review (read-only)

From the repository root, confirm the selected Azure subscription and check the actual setting:

```sh
az account show --output table
az postgres flexible-server parameter show --resource-group rg-planarian --server-name db-planarian --name azure.extensions --query '{value:value,source:source}' --output json
az deployment group what-if --resource-group rg-planarian --template-file infrastructure/postgresql-extensions.bicep --parameters serverName=db-planarian
```

Stop if What-If shows changes outside `db-planarian/configurations/azure.extensions`, or the live allow-list has unexpected entries. What-If is advisory, not a guarantee.

## Deployment (manual only)

Once the What-If output is reviewed and approved:

```sh
az deployment group create --resource-group rg-planarian --template-file infrastructure/postgresql-extensions.bicep --parameters serverName=db-planarian
```

Then verify `azure.extensions` again. EF Core still owns `CREATE EXTENSION pg_trgm` and the search schema; run the migration only after Azure's allow-list is ready.

Adding these files does **not** apply the configuration.
