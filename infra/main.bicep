targetScope = 'subscription'

@description('Short name for the environment, e.g. dev or prod.')
param environment string

@description('Azure region for all resources.')
param location string

@description('Base name used when composing all resource names.')
param appName string

var resourceGroupName = 'rg-${appName}-${environment}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module storage './modules/storage.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
  }
}

module cosmos './modules/cosmos.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
  }
}

module functions './modules/functions.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
    storageAccountName: storage.outputs.accountName
    storageAccountBlobEndpoint: storage.outputs.blobEndpoint
    cosmosAccountEndpoint: cosmos.outputs.accountEndpoint
  }
}

// Grant the Function App's managed identity the roles it needs on Storage and Cosmos DB.
// This module runs after functions is deployed so the principalId is available.
module roleAssignments './modules/roleAssignments.bicep' = {
  scope: resourceGroup
  params: {
    storageAccountName: storage.outputs.accountName
    cosmosAccountName: cosmos.outputs.accountName
    functionAppPrincipalId: functions.outputs.principalId
  }
}
