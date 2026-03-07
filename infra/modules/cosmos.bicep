@description('Base name used when composing all resource names.')
param appName string

@description('Short environment name, e.g. dev or prod.')
param environment string

@description('Azure region for the Cosmos DB account.')
param location string

var cosmosAccountName = 'cosmos-${appName}-${environment}'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  tags: {
    app: appName
    environment: environment
  }
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    // Serverless: pay-per-request, zero cost at idle — ideal for dev/toy workloads
    capabilities: [
      { name: 'EnableServerless' }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: 'ticketflow'
  properties: {
    resource: {
      id: 'ticketflow'
    }
  }
}

resource eventsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'events'
  properties: {
    resource: {
      id: 'events'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
  }
}

@description('Cosmos DB account name — used for data-plane role assignments.')
output accountName string = cosmosAccount.name

@description('Cosmos DB endpoint URL — used by the Function App to connect via managed identity.')
output accountEndpoint string = cosmosAccount.properties.documentEndpoint
