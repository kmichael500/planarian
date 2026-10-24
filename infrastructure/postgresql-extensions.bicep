targetScope = 'resourceGroup'

@description('Name of the existing Azure Database for PostgreSQL flexible server.')
param serverName string = 'db-planarian'

// Reference only: never redeploy the parent server or unrelated settings.
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2022-12-01' existing = {
  name: serverName
}

// Complete allow-list, not an append; preserve existing extensions.
resource extensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2022-12-01' = {
  parent: postgres
  name: 'azure.extensions'
  properties: {
    source: 'user-override'
    value: 'POSTGIS,PG_TRGM'
  }
}
